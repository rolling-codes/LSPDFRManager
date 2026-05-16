using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Install;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public enum InstallOutcome { None, Cancelled, FailedNoMutation, FailedRestored, FailedPartial }

public class InstallViewModel : ObservableObject, IDisposable
{
    private readonly InstallQueue _queue = InstallQueue.Instance;
    private readonly IUserPromptService _promptService;
    private readonly IInstallController _installController;
    private readonly Action<ModInfo> _installStartedHandler;
    private readonly Action<InstalledMod> _installCompletedHandler;
    private readonly Action<ModInfo, InstallResult> _installFailedHandler;
    private readonly Action<string> _bridgeDetectingHandler;
    private readonly Action<ModInfo> _bridgeStagedHandler;
    private readonly Action<string, string> _bridgeFailedHandler;
    private bool _disposed;

    private string? _droppedPath;
    private ModInfo? _detectedMod;
    private InstallPlan? _reviewPlan;
    private bool _isDetecting;
    private bool _isInstalling;
    private bool _isBuildingPlan;
    private string _authorOverride = "";
    private string _nameOverride = "";
    private string? _lastErrorMessage;
    private InstallOutcome _lastOutcome = InstallOutcome.None;
    private string? _suggestedAction;

    public InstallViewModel(IUserPromptService? promptService = null, IInstallController? installController = null)
    {
        _promptService = promptService ?? new UserPromptService();
        _installController = installController ?? new InstallWorkflowController();
        BrowseCommand = new RelayCommand(BrowseForArchive);

        // Opens the pre-install review panel — builds the plan but does NOT write any files.
        InstallCommand = new RelayCommand(
            () => _ = ExecuteInstallCommandAsync(),
            () => DetectedMod is not null && IsIdle);

        // Confirmed by user from the review panel — actually enqueues the install.
        ConfirmInstallCommand = new RelayCommand(
            () => _ = ExecuteConfirmCommandAsync(),
            () => ReviewPlan is not null && ReviewCanConfirm && IsIdle);

        CancelReviewCommand = new RelayCommand(() =>
        {
            ReviewPlan = null;
            LastErrorMessage = null;
        });

        ClearCommand = new RelayCommand(Clear);
        ClearLogCommand = new RelayCommand(Log.Clear);

        _installStartedHandler = mod => AddLog($"Installing {mod.Name}…");
        _installCompletedHandler = mod => UiDispatcher.Invoke(() =>
        {
            IsInstalling = false;
            AddLog($"✓ Installed: {mod.Name}");
        });
        _installFailedHandler = (mod, result) => UiDispatcher.Invoke(() =>
        {
            IsInstalling = false;
            ReportInstallFailure(mod, result);
        });

        _queue.InstallStarted += _installStartedHandler;
        _queue.InstallCompleted += _installCompletedHandler;
        _queue.InstallFailedWithResult += _installFailedHandler;

        var bridge = ModDownloadBridge.Instance;
        _bridgeDetectingHandler = name => AddLog($"[Browse] Detecting: {name}…");
        _bridgeStagedHandler = StageDownloadedMod;
        _bridgeFailedHandler = (name, err) => AddLog($"[Browse] Failed: {name} — {err}");

        bridge.Detecting += _bridgeDetectingHandler;
        bridge.Staged    += _bridgeStagedHandler;
        bridge.Failed    += _bridgeFailedHandler;
    }

    public event Action<string>? LogAdded;

    public ObservableCollection<string> Log { get; } = [];

    public string? DroppedPath
    {
        get => _droppedPath;
        set => SetProperty(ref _droppedPath, value);
    }

    public ModInfo? DetectedMod
    {
        get => _detectedMod;
        set
        {
            if (!SetProperty(ref _detectedMod, value))
                return;

            OnPropertyChanged(nameof(HasDetection));
            OnPropertyChanged(nameof(HasNoDetection));
            OnPropertyChanged(nameof(ShowEmptyDetection));
            OnPropertyChanged(nameof(DetectedFilesSample));
            OnPropertyChanged(nameof(HasMoreDetectedFiles));
            OnPropertyChanged(nameof(DetectedFilesMoreText));
        }
    }

    public bool IsDetecting
    {
        get => _isDetecting;
        set
        {
            if (SetProperty(ref _isDetecting, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(ShowEmptyDetection));
            }
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            if (SetProperty(ref _isInstalling, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsDetecting && !IsInstalling && !_isBuildingPlan;
    public bool HasDetection => DetectedMod is not null;
    public bool HasNoDetection => DetectedMod is null;
    public bool ShowEmptyDetection => DetectedMod is null && !IsDetecting;
    public bool ShowDetectionPanel => HasDetection && !IsReviewing;
    public bool HasLastError => !string.IsNullOrWhiteSpace(LastErrorMessage);

    public InstallOutcome LastOutcome
    {
        get => _lastOutcome;
        private set
        {
            SetProperty(ref _lastOutcome, value);
            OnPropertyChanged(nameof(OutcomeIsPartial));
            OnPropertyChanged(nameof(OutcomeIsRestored));
            OnPropertyChanged(nameof(OutcomeIsSimpleFailure));
        }
    }

    public bool OutcomeIsPartial      => _lastOutcome == InstallOutcome.FailedPartial;
    public bool OutcomeIsRestored     => _lastOutcome == InstallOutcome.FailedRestored;
    public bool OutcomeIsSimpleFailure => _lastOutcome is InstallOutcome.Cancelled or InstallOutcome.FailedNoMutation;

    public string? SuggestedAction
    {
        get => _suggestedAction;
        private set
        {
            SetProperty(ref _suggestedAction, value);
            OnPropertyChanged(nameof(HasSuggestedAction));
        }
    }

    public bool HasSuggestedAction => !string.IsNullOrEmpty(_suggestedAction);

    /// <summary>
    /// Pure classification of an <see cref="InstallResult"/> into a UI outcome tier.
    /// Separated from the VM so it can be tested independently.
    /// </summary>
    public static (InstallOutcome Outcome, string ErrorMessage, string? SuggestedAction)
        ClassifyFailure(InstallResult result)
    {
        var error = result.UserMessage ?? result.Error ?? "Unknown error";
        var isCancelled = error.Contains("cancel", StringComparison.OrdinalIgnoreCase);

        if (result.FailureCategory == InstallFailureCategory.PermissionDenied)
        {
            return (
                InstallOutcome.FailedNoMutation,
                $"Install failed: {error}",
                "Run LSPDFR Manager as administrator or grant write access to your GTA V folder."
            );
        }

        if (!result.IsPartial)
        {
            var outcome = isCancelled ? InstallOutcome.Cancelled : InstallOutcome.FailedNoMutation;
            return (outcome, $"Install failed: {error}", null);
        }

        if (result.RollbackErrors.Count == 0)
            return (InstallOutcome.FailedRestored, $"Install failed: {error}", null);

        return (
            InstallOutcome.FailedPartial,
            $"Install failed: {error}",
            $"Rollback incomplete — {result.RollbackErrors.Count} file(s) could not be cleaned up. Verify your GTA V folder manually."
        );
    }

    // ── Review plan state ────────────────────────────────────────────────────

    public InstallPlan? ReviewPlan
    {
        get => _reviewPlan;
        private set
        {
            SetProperty(ref _reviewPlan, value);
            OnPropertyChanged(nameof(IsReviewing));
            OnPropertyChanged(nameof(ShowDetectionPanel));
            OnPropertyChanged(nameof(ReviewCanConfirm));
            OnPropertyChanged(nameof(ReviewHasBlockingIssues));
            OnPropertyChanged(nameof(ReviewHasWarnings));
            OnPropertyChanged(nameof(ReviewWillInstall));
            OnPropertyChanged(nameof(ReviewWillOverwrite));
            OnPropertyChanged(nameof(ReviewSuspicious));
            OnPropertyChanged(nameof(ReviewBlocked));
            OnPropertyChanged(nameof(ReviewJunk));
            OnPropertyChanged(nameof(ReviewWillInstallCount));
            OnPropertyChanged(nameof(ReviewWillOverwriteCount));
            OnPropertyChanged(nameof(ReviewSuspiciousCount));
            OnPropertyChanged(nameof(ReviewBlockedCount));
            OnPropertyChanged(nameof(ReviewJunkCount));
            OnPropertyChanged(nameof(ReviewModTypeLabel));
            OnPropertyChanged(nameof(ReviewConfidenceLabel));
            OnPropertyChanged(nameof(ReviewIsMixed));
            OnPropertyChanged(nameof(ReviewIsUnknown));
            OnPropertyChanged(nameof(ReviewSecondaryTypeLabels));
            OnPropertyChanged(nameof(ReviewHasSecondaryTypes));
            OnPropertyChanged(nameof(ReviewEvidence));
            OnPropertyChanged(nameof(ReviewHasEvidence));
            OnPropertyChanged(nameof(ReviewDependencyWarnings));
            OnPropertyChanged(nameof(ReviewHasDependencyWarnings));
            OnPropertyChanged(nameof(ReviewShowDependencyWarnings));
            OnPropertyChanged(nameof(ReviewGeneralWarnings));
            OnPropertyChanged(nameof(ReviewHasGeneralWarnings));
            OnPropertyChanged(nameof(ReviewMissingDependencies));
            OnPropertyChanged(nameof(ReviewPresentDependencies));
            OnPropertyChanged(nameof(ReviewUnknownDependencies));
            OnPropertyChanged(nameof(ReviewNotApplicableDependencies));
            OnPropertyChanged(nameof(ReviewHasMissingDependencies));
            OnPropertyChanged(nameof(ReviewHasPresentDependencies));
            OnPropertyChanged(nameof(ReviewHasUnknownDependencies));
            OnPropertyChanged(nameof(ReviewHasNotApplicableDependencies));
            OnPropertyChanged(nameof(ReviewIsOivPrimary));
            OnPropertyChanged(nameof(ReviewIsOivSecondary));
            OnPropertyChanged(nameof(ReviewOivMetadata));
            OnPropertyChanged(nameof(ReviewHasOivMetadata));
            OnPropertyChanged(nameof(ReviewOivName));
            OnPropertyChanged(nameof(ReviewOivVersion));
            OnPropertyChanged(nameof(ReviewOivAuthor));
            OnPropertyChanged(nameof(ReviewOivDescription));
            OnPropertyChanged(nameof(ReviewOivTargetGame));
            OnPropertyChanged(nameof(ReviewOivFileCount));
            OnPropertyChanged(nameof(ReviewOivTargetPaths));
        }
    }

    public bool IsReviewing => _reviewPlan is not null;
    public bool ReviewCanConfirm => _reviewPlan is not null && _reviewPlan.BlockingIssues.Count == 0;
    public bool ReviewHasBlockingIssues => (_reviewPlan?.BlockingIssues.Count ?? 0) > 0;
    public bool ReviewHasWarnings => (_reviewPlan?.Warnings.Count ?? 0) > 0;

    public IEnumerable<InstallPlanEntry> ReviewWillInstall =>
        _reviewPlan?.Entries.Where(e =>
            e.Risk != InstallRisk.Incompatible &&
            !e.WillOverwrite &&
            e.PlannedAction != InstallConflictAction.Skip &&
            !InstallerSafetyPolicy.IsJunkEntry(e.ArchivePath)) ?? [];

    public IEnumerable<InstallPlanEntry> ReviewWillOverwrite =>
        _reviewPlan?.Entries.Where(e =>
            e.WillOverwrite &&
            e.Risk != InstallRisk.Incompatible &&
            e.PlannedAction != InstallConflictAction.Skip &&
            !InstallerSafetyPolicy.IsJunkEntry(e.ArchivePath)) ?? [];

    public IEnumerable<InstallPlanEntry> ReviewSuspicious =>
        _reviewPlan?.Entries.Where(e => e.Risk == InstallRisk.Suspicious) ?? [];

    public IEnumerable<InstallPlanEntry> ReviewBlocked =>
        _reviewPlan?.Entries.Where(e => e.Risk == InstallRisk.Incompatible) ?? [];

    public IEnumerable<InstallPlanEntry> ReviewJunk =>
        _reviewPlan?.Entries.Where(e => InstallerSafetyPolicy.IsJunkEntry(e.ArchivePath)) ?? [];

    public int ReviewWillInstallCount => ReviewWillInstall.Count();
    public int ReviewWillOverwriteCount => ReviewWillOverwrite.Count();
    public int ReviewSuspiciousCount => ReviewSuspicious.Count();
    public int ReviewBlockedCount => ReviewBlocked.Count();
    public int ReviewJunkCount => ReviewJunk.Count();

    public bool ReviewHasOverwrites => ReviewWillOverwriteCount > 0;
    public bool ReviewHasSuspicious => ReviewSuspiciousCount > 0;
    public bool ReviewHasBlocked => ReviewBlockedCount > 0;
    public bool ReviewHasJunk => ReviewJunkCount > 0;

    // ── Mod intelligence (type + dependency) ─────────────────────────────────

    private static string ModTypeLabel(ModType type) => type switch
    {
        ModType.AsiMod       => "ASI Mod",
        ModType.Script       => "Script",
        ModType.LspdfrPlugin => "LSPDFR Plugin",
        ModType.OivPackage   => "OIV Package",
        ModType.Eup          => "EUP Clothing",
        ModType.VehicleDlc   => "DLC / Vehicle",
        ModType.Map          => "Map / MLO",
        ModType.Sound        => "Sound Pack",
        ModType.ConfigPreset => "Config",
        _                           => "Unknown",
    };

    public string ReviewModTypeLabel =>
        _reviewPlan?.ModTypeResult is { PrimaryType: var t } && t != ModType.Unknown
            ? ModTypeLabel(t)
            : "Unknown";

    public string ReviewConfidenceLabel =>
        _reviewPlan?.ModTypeResult?.ConfidenceLabel ?? "";

    public bool ReviewIsMixed => _reviewPlan?.ModTypeResult?.IsMixed ?? false;

    public bool ReviewIsUnknown =>
        (_reviewPlan?.ModTypeResult?.PrimaryType ?? ModType.Unknown) == ModType.Unknown;

    public IEnumerable<string> ReviewSecondaryTypeLabels =>
        _reviewPlan?.ModTypeResult?.SecondaryTypes
            .Select(s => ModTypeLabel(s.Type)) ?? [];

    public bool ReviewHasSecondaryTypes =>
        _reviewPlan?.ModTypeResult?.SecondaryTypes.Count > 0;

    public IEnumerable<string> ReviewEvidence =>
        _reviewPlan?.ModTypeResult?.Evidence ?? [];

    public bool ReviewHasEvidence =>
        _reviewPlan?.ModTypeResult?.Evidence.Count > 0;

    /// <summary>
    /// Dependency warnings from the plan — strips the "Dependency: " prefix added by SmartInstallPlanner.
    /// </summary>
    public IEnumerable<string> ReviewDependencyWarnings =>
        _reviewPlan?.Warnings
            .Where(w => w.StartsWith("Dependency:", StringComparison.Ordinal))
            .Select(w => w["Dependency:".Length..].TrimStart(' ', '—', ' '))
        ?? [];

    public bool ReviewHasDependencyWarnings =>
        _reviewPlan?.Warnings.Any(w => w.StartsWith("Dependency:", StringComparison.Ordinal)) ?? false;

    /// <summary>
    /// Show legacy dependency warnings only when no probe result is available.
    /// Probe results supersede the raw warning strings.
    /// </summary>
    public bool ReviewShowDependencyWarnings =>
        ReviewHasDependencyWarnings &&
        (_reviewPlan?.ProbeResult is null || _reviewPlan.ProbeResult.Probes.Count == 0);

    // ── Probe-based dependency status ────────────────────────────────────────

    public IEnumerable<DependencyProbe> ReviewMissingDependencies =>
        _reviewPlan?.ProbeResult?.Probes.Where(p => p.Status == DependencyProbeStatus.Missing) ?? [];

    public IEnumerable<DependencyProbe> ReviewPresentDependencies =>
        _reviewPlan?.ProbeResult?.Probes.Where(p => p.Status == DependencyProbeStatus.Present) ?? [];

    public IEnumerable<DependencyProbe> ReviewUnknownDependencies =>
        _reviewPlan?.ProbeResult?.Probes.Where(p => p.Status == DependencyProbeStatus.Unknown) ?? [];

    public IEnumerable<DependencyProbe> ReviewNotApplicableDependencies =>
        _reviewPlan?.ProbeResult?.Probes.Where(p => p.Status == DependencyProbeStatus.NotApplicable) ?? [];

    public bool ReviewHasMissingDependencies =>
        _reviewPlan?.ProbeResult?.HasMissingRequired ?? false;

    public bool ReviewHasPresentDependencies => ReviewPresentDependencies.Any();
    public bool ReviewHasUnknownDependencies => ReviewUnknownDependencies.Any();
    public bool ReviewHasNotApplicableDependencies => ReviewNotApplicableDependencies.Any();

    // ── OIV guardrail ────────────────────────────────────────────────────────

    /// <summary>True when the detected primary type is OIV — blocks normal install.</summary>
    public bool ReviewIsOivPrimary =>
        _reviewPlan?.ModTypeResult?.PrimaryType == ModType.OivPackage;

    /// <summary>True when OIV appears as a secondary/mixed type but is not the primary.</summary>
    public bool ReviewIsOivSecondary =>
        !ReviewIsOivPrimary &&
        (_reviewPlan?.ModTypeResult?.SecondaryTypes.Any(t => t.Type == ModType.OivPackage) ?? false);

    public OivPackage? ReviewOivMetadata => _reviewPlan?.OivMetadata;
    public bool ReviewHasOivMetadata => _reviewPlan?.OivMetadata?.IsValid == true;
    public string ReviewOivName => _reviewPlan?.OivMetadata?.Name ?? "";
    public string ReviewOivVersion => _reviewPlan?.OivMetadata?.Version ?? "";
    public string ReviewOivAuthor => _reviewPlan?.OivMetadata?.Author ?? "";
    public string ReviewOivDescription => _reviewPlan?.OivMetadata?.Description ?? "";
    public string ReviewOivTargetGame => _reviewPlan?.OivMetadata?.TargetGame ?? "";
    public int ReviewOivFileCount => _reviewPlan?.OivMetadata?.Files.Count ?? 0;
    public IEnumerable<string> ReviewOivTargetPaths =>
        _reviewPlan?.OivMetadata?.Files.Select(f => f.InstallPath) ?? [];

    /// <summary>Non-dependency warnings (path conflicts, overwrites, etc.).</summary>
    public IEnumerable<string> ReviewGeneralWarnings =>
        _reviewPlan?.Warnings
            .Where(w => !w.StartsWith("Dependency:", StringComparison.Ordinal))
        ?? [];

    public bool ReviewHasGeneralWarnings =>
        _reviewPlan?.Warnings.Any(w => !w.StartsWith("Dependency:", StringComparison.Ordinal)) ?? false;

    public string? LastErrorMessage
    {
        get => _lastErrorMessage;
        set
        {
            if (SetProperty(ref _lastErrorMessage, value))
                OnPropertyChanged(nameof(HasLastError));
        }
    }

    public string AuthorOverride
    {
        get => _authorOverride;
        set => SetProperty(ref _authorOverride, value);
    }

    public string NameOverride
    {
        get => _nameOverride;
        set => SetProperty(ref _nameOverride, value);
    }

    public IEnumerable<string> DetectedFilesSample =>
        DetectedMod?.Files.Take(10) ?? [];

    public bool HasMoreDetectedFiles =>
        (DetectedMod?.Files.Count ?? 0) > 10;

    public string DetectedFilesMoreText =>
        HasMoreDetectedFiles
            ? $"+{DetectedMod!.Files.Count - 10} more files"
            : "";

    public ICommand BrowseCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand ConfirmInstallCommand { get; }
    public ICommand CancelReviewCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ClearLogCommand { get; }

    private void StageDownloadedMod(ModInfo mod)
    {
        UiDispatcher.Invoke(() =>
        {
            DroppedPath = mod.SourcePath;
            DetectedMod = mod;
            ReviewPlan = null;
            LastErrorMessage = null;
            NameOverride = mod.Name;
            AuthorOverride = mod.Author ?? "";
            AddLog($"[Browse] Staged for review: {mod.Name} ({mod.TypeLabel})");
        });
    }

    public async Task DetectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        DroppedPath = path;
        DetectedMod = null;
        IsDetecting = true;

        try
        {
            var detected = await _installController.DetectAsync(path, NameOverride, AuthorOverride);

            UiDispatcher.Invoke(() =>
            {
                DetectedMod = detected;
                NameOverride = detected.Name;
                AuthorOverride = detected.Author ?? "";
                AddLog($"→ {detected.TypeLabel} (confidence: {detected.ConfidenceLabel})");
            });

            AddLog($"Staged for review: {detected.Name}");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsDetecting = false);
        }
    }

    /// <summary>
    /// Detects multiple archives in parallel and stages them for explicit review.
    /// Used when the user drops several files at once onto the drop zone.
    /// </summary>
    public async Task DetectBatchAsync(IEnumerable<string> paths)
    {
        var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (validPaths.Count == 0) return;

        if (validPaths.Count == 1)
        {
            await DetectAsync(validPaths[0]);
            return;
        }

        IsDetecting = true;
        AddLog($"Detecting {validPaths.Count} mods in parallel…");

        try
        {
            var results = await _installController.DetectBatchAsync(validPaths);

            foreach (var detected in results)
                AddLog($"→ {detected.Name} — {detected.TypeLabel} ({detected.ConfidenceLabel})");

            var selected = results.FirstOrDefault();
            if (selected is not null)
            {
                UiDispatcher.Invoke(() =>
                {
                    DroppedPath = selected.SourcePath;
                    DetectedMod = selected;
                    ReviewPlan = null;
                    LastErrorMessage = null;
                    NameOverride = selected.Name;
                    AuthorOverride = selected.Author ?? "";
                });
                AddLog($"Staged for review: {selected.Name}");
            }

            if (results.Count > 1)
                AddLog("Multiple archives detected; install each mod with the Install button after review.");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsDetecting = false);
        }
    }

    /// <summary>
    /// Builds an install plan and shows the review panel. No files are written.
    /// </summary>
    private async Task BuildReviewPlanAsync()
    {
        if (DetectedMod is null)
            return;

        if (!TryValidatePreInstall(out var validationMessage))
        {
            LastErrorMessage = validationMessage;
            AddLog($"✗ {validationMessage}");
            return;
        }

        LastErrorMessage = null;
        DetectedMod.Name = string.IsNullOrWhiteSpace(NameOverride) ? DetectedMod.Name : NameOverride.Trim();
        DetectedMod.Author = string.IsNullOrWhiteSpace(AuthorOverride) ? null : AuthorOverride.Trim();

        if (!ResolveDuplicateBeforeInstall())
            return;

        _isBuildingPlan = true;
        OnPropertyChanged(nameof(IsIdle));
        AddLog($"Building install plan for {DetectedMod.Name}…");

        try
        {
            var plan = await _installController.BuildReviewPlanAsync(DetectedMod);

            UiDispatcher.Invoke(() =>
            {
                ReviewPlan = plan;
                AddLog($"Plan ready: {plan.Entries.Count} entries — confirm to install.");
            });
        }
        finally
        {
            UiDispatcher.Invoke(() =>
            {
                _isBuildingPlan = false;
                OnPropertyChanged(nameof(IsIdle));
            });
        }
    }

    /// <summary>
    /// User confirmed the review panel — enqueues the install.
    /// </summary>
    private async Task ConfirmInstallAsync()
    {
        if (DetectedMod is null || ReviewPlan is null)
            return;

        var gtaPath = AppConfig.Instance.GtaPath;
        if (!TryValidatePreInstall(out var validationMessage))
        {
            LastErrorMessage = validationMessage;
            AddLog($"✗ {validationMessage}");
            return;
        }

        // Pre-install conflict check against tracked mods (independent of file-level plan)
        var incomingPaths = DetectedMod.Files
            .Select(f => Path.GetFullPath(Path.Combine(gtaPath, f.Replace('/', Path.DirectorySeparatorChar))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conflictingModsWithFiles = ModLibraryService.Instance.Mods
            .Where(mod => mod.InstalledFiles.Any(file =>
                incomingPaths.Contains(Path.GetFullPath(NormalizeDisabledPath(file)))))
            .ToList();

        var conflictingMods = conflictingModsWithFiles
            .Select(mod => $"  • {mod.Name}{(mod.IsEnabled ? "" : " (disabled)")}")
            .ToList();

        var pathsCoveredByMod = conflictingModsWithFiles
            .SelectMany(mod => mod.InstalledFiles.Select(f => Path.GetFullPath(NormalizeDisabledPath(f))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var disabledFileConflicts = incomingPaths
            .Where(path => File.Exists(path + ".disabled") && !pathsCoveredByMod.Contains(path))
            .Select(path => $"  • {Path.GetFileName(path)} (disabled file exists)")
            .ToList();

        conflictingMods.AddRange(disabledFileConflicts);

        if (conflictingMods.Count > 0)
        {
            var modList = string.Join("\n", conflictingMods);
            var msg = $"The following installed mods share files with '{DetectedMod.Name}':\n\n{modList}\n\nInstalling may overwrite their files. Continue anyway?";
            var result = _promptService.Show(msg, "File Conflict Detected",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;
        }

        ReviewPlan = null;
        IsInstalling = true;
        AddLog($"Queued: {DetectedMod.Name}");

        var confirmation = await _installController.ConfirmInstallAsync(DetectedMod, gtaPath);

        if (confirmation.RequiresLspdfrPostInstallCheck && confirmation.PostInstallModName is not null)
            ScheduleLspdfrPostInstallCheck(confirmation.PostInstallModName, confirmation.GtaPath);
    }

    private void ScheduleLspdfrPostInstallCheck(string modName, string gtaPath)
    {
        void OnCompleted(InstalledMod completedMod)
        {
            if (!completedMod.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
                return;

            _queue.InstallCompleted -= OnCompleted;

            UiDispatcher.Invoke(() =>
            {
                var validation = LspdfrInstallService.ValidatePostInstall(gtaPath);

                if (validation.IsValid)
                {
                    AddLog("✓ LSPDFR layout validated — all required paths present.");
                    if (validation.RageLogAnalysis.LogFound && !validation.RageLogAnalysis.HasCriticalErrors)
                        AddLog("✓ RAGE log: no critical errors detected.");
                }
                else
                {
                    foreach (var p in validation.MissingPaths)
                        AddLog($"  ✗ Missing: {p}");
                    foreach (var p in validation.DoubleNestedPaths)
                        AddLog($"  ✗ Double-nested: {p}");
                    if (validation.RageLogAnalysis.HasCriticalErrors)
                    {
                        AddLog("  ⚠ RAGE log errors detected:");
                        foreach (var e in validation.RageLogAnalysis.RecentErrors.Take(5))
                            AddLog($"    {e}");
                    }
                }
            });
        }

        _queue.InstallCompleted += OnCompleted;
    }

    private bool ResolveDuplicateBeforeInstall()
    {
        if (DetectedMod is null)
            return false;

        var duplicates = ModLibraryService.Instance.Mods
            .Where(mod => mod.Name.Equals(DetectedMod.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (duplicates.Count == 0)
            return true;

        var duplicateList = string.Join("\n", duplicates.Select(mod => $"  • {mod.Name} ({mod.TypeLabel})"));
        var message =
            $"A mod with this name is already installed:\n\n{duplicateList}\n\n" +
            "Choose Yes to replace the installed copy, No to install this as a separate entry, or Cancel to skip.";

        var result = _promptService.Show(
            message,
            "Duplicate Mod Detected",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Cancel)
        {
            AddLog($"Skipped duplicate: {DetectedMod.Name}");
            return false;
        }

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            foreach (var duplicate in duplicates)
                ModLibraryService.Instance.Uninstall(duplicate.Id);

            AddLog($"Replacing duplicate install: {DetectedMod.Name}");
        }
        else
        {
            AddLog($"Installing duplicate as separate entry: {DetectedMod.Name}");
        }

        return true;
    }

    private static string NormalizeDisabledPath(string path) =>
        path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path[..^".disabled".Length]
            : path;

    private void BrowseForArchive()
    {
        if (!_promptService.TrySelectModArchive(out var fileName))
        {
            AddLog("Archive selection cancelled.");
            return;
        }

        _ = DetectArchiveFromBrowseAsync(fileName);
    }

    private async Task DetectArchiveFromBrowseAsync(string fileName)
    {
        try
        {
            await DetectAsync(fileName);
        }
        catch (Exception ex)
        {
            UiDispatcher.Invoke(() => LastErrorMessage = $"Detection failed: {ex.Message}");
        }
    }

    private async Task ExecuteInstallCommandAsync()
    {
        try
        {
            await BuildReviewPlanAsync();
        }
        catch (Exception ex)
        {
            UiDispatcher.Invoke(() =>
            {
                _isBuildingPlan = false;
                OnPropertyChanged(nameof(IsIdle));
                LastErrorMessage = $"Plan failed: {ex.Message}";
            });
            AppLogger.Error("[INSTALL_VM_PLAN_EXCEPTION]", ex);
        }
    }

    private async Task ExecuteConfirmCommandAsync()
    {
        try
        {
            await ConfirmInstallAsync();
        }
        catch (Exception ex)
        {
            UiDispatcher.Invoke(() =>
            {
                IsInstalling = false;
                LastErrorMessage = $"Install failed: {ex.Message}";
            });
            AppLogger.Error("[INSTALL_VM_CONFIRM_EXCEPTION]", ex);
        }
    }

    private bool TryValidatePreInstall(out string message)
    {
        if (DetectedMod is null)
        {
            message = "No mod/package is selected for install.";
            return false;
        }

        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrWhiteSpace(gtaPath))
        {
            message = "Set your GTA V folder in Settings before installing.";
            return false;
        }

        if (!Directory.Exists(gtaPath))
        {
            message = "Configured GTA V folder was not found. Update it in Settings.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DetectedMod.SourcePath))
        {
            message = "Selected mod archive path is missing.";
            return false;
        }

        if (!File.Exists(DetectedMod.SourcePath) && !Directory.Exists(DetectedMod.SourcePath))
        {
            message = "Selected mod file/folder was not found. Re-select it and try again.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void Clear()
    {
        DroppedPath = null;
        DetectedMod = null;
        AuthorOverride = "";
        NameOverride = "";
        LastErrorMessage = null;
        LastOutcome = InstallOutcome.None;
        SuggestedAction = null;
        Log.Clear();
    }

    private void ReportInstallFailure(ModInfo mod, InstallResult result)
    {
        var (outcome, errorMessage, suggestedAction) = ClassifyFailure(result);
        var displayError = result.UserMessage ?? result.Error ?? "Unknown error";

        AddLog($"✗ Failed: {mod.Name} — {displayError}");

        if (result.IsPartial)
        {
            if (result.RollbackErrors.Count == 0)
            {
                AddLog("  ↩ Rollback completed — filesystem restored.");
            }
            else
            {
                AddLog($"  ⚠ Rollback partial — {result.RollbackErrors.Count} cleanup action(s) failed.");
                foreach (var rollbackError in result.RollbackErrors)
                    AddLog($"    • {rollbackError}");
            }
        }

        LastOutcome = outcome;
        LastErrorMessage = errorMessage;
        SuggestedAction = suggestedAction;
    }

    private void AddLog(string message)
    {
        var maxLogEntries = Math.Max(25, AppConfig.Instance.MaxInstallLogEntries);
        UiDispatcher.Invoke(() =>
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (Log.Count > maxLogEntries)
                Log.RemoveAt(0);
            LogAdded?.Invoke(message);
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _queue.InstallStarted -= _installStartedHandler;
        _queue.InstallCompleted -= _installCompletedHandler;
        _queue.InstallFailedWithResult -= _installFailedHandler;

        var bridge = ModDownloadBridge.Instance;
        bridge.Detecting -= _bridgeDetectingHandler;
        bridge.Staged -= _bridgeStagedHandler;
        bridge.Failed -= _bridgeFailedHandler;

        _disposed = true;
    }
}
