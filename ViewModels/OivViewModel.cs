using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.Features.OivCreatorTemplates;
using LSPDFRManager.Features.OivCreatorTemplates.Models;

namespace LSPDFRManager.ViewModels;

public class OivViewModel : ObservableObject
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly IOivSourceScanner    _scanner   = new OivSourceScanner();
    private readonly IOivPackageValidator _validator = new OivPackageValidator();
    private readonly IOivPackageBuilder   _builder   = new OivPackageBuilder();
    private readonly IOivTemplateController _templateController = new OivTemplateController();
    private readonly IFileDialogService _fileDialogService;

    // ── Mode ──────────────────────────────────────────────────────────────────
    private bool _isCreatorMode = true;

    public bool IsCreatorMode
    {
        get => _isCreatorMode;
        set
        {
            if (SetProperty(ref _isCreatorMode, value))
                OnPropertyChanged(nameof(IsInstallerMode));
        }
    }

    public bool IsInstallerMode => !_isCreatorMode;

    // ── Shared ────────────────────────────────────────────────────────────────
    private bool   _isWorking;
    private string _statusMessage = "";

    public bool IsWorking
    {
        get => _isWorking;
        private set => SetProperty(ref _isWorking, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // =========================================================================
    // CREATOR — wizard (steps 0 = Edit, 1 = Review, 2 = Done)
    // =========================================================================

    private int            _creatorStep;
    private string         _creatorName        = "";
    private string         _creatorVersion     = "1.0";
    private string         _creatorAuthor      = "";
    private string         _creatorDescription = "";
    private OivPackageKind _creatorKind        = OivPackageKind.Basic;
    private string         _creatorOutputPath  = "";
    private OivPackagePlan? _creatorPlan;
    private RelayCommand   _buildPlanCommand   = null!;
    private RelayCommand   _exportCommand      = null!;
    private string         _exportStatus       = "";

    // ── Wizard step ───────────────────────────────────────────────────────────

    public int CreatorStep
    {
        get => _creatorStep;
        private set
        {
            if (SetProperty(ref _creatorStep, value))
            {
                OnPropertyChanged(nameof(IsCreatorStep0));
                OnPropertyChanged(nameof(IsCreatorStep1));
                OnPropertyChanged(nameof(IsCreatorStep2));
            }
        }
    }

    public bool IsCreatorStep0 => _creatorStep == 0;
    public bool IsCreatorStep1 => _creatorStep == 1;
    public bool IsCreatorStep2 => _creatorStep == 2;

    // ── Metadata fields ───────────────────────────────────────────────────────

    public string CreatorName
    {
        get => _creatorName;
        set
        {
            if (SetProperty(ref _creatorName, value))
            {
                OnPropertyChanged(nameof(CanBuildPlan));
                RefreshCreatorCommands();
            }
        }
    }

    public string CreatorVersion
    {
        get => _creatorVersion;
        set => SetProperty(ref _creatorVersion, value);
    }

    public string CreatorAuthor
    {
        get => _creatorAuthor;
        set => SetProperty(ref _creatorAuthor, value);
    }

    public string CreatorDescription
    {
        get => _creatorDescription;
        set => SetProperty(ref _creatorDescription, value);
    }

    public OivPackageKind CreatorKind
    {
        get => _creatorKind;
        set => SetProperty(ref _creatorKind, value);
    }

    // -- OIV Template System (#37) --
    private OivTemplateId _selectedTemplateId = OivTemplateId.None;
    private OivTemplateDefinition? _selectedTemplate;
    private string? _templateApplyStatus;

    // Pre-apply snapshot for 1-level undo
    private Dictionary<string, string>? _preApplyMetadata;
    private Dictionary<string, string>? _preApplyPaths; // SourcePath -> InstallPath

    public OivTemplateId SelectedTemplateId
    {
        get => _selectedTemplateId;
        set => SetProperty(ref _selectedTemplateId, value); // NO SIDE EFFECTS HERE
    }

    /// <summary>
    /// Bound to ComboBox.SelectedItem. Maps to/from SelectedTemplateId.
    /// Setting this has ZERO side effects — no metadata mutation.
    /// </summary>
    public OivTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
                SelectedTemplateId = value?.Id ?? OivTemplateId.None;
        }
    }

    public IReadOnlyList<OivTemplateDefinition> AvailableTemplates { get; } = new OivTemplateController().GetAvailableTemplates();

    public ICommand ApplyTemplateCommand { get; }
    public ICommand UndoApplyTemplateCommand { get; }

    public string? TemplateApplyStatus
    {
        get => _templateApplyStatus;
        private set => SetProperty(ref _templateApplyStatus, value);
    }

    public bool CanUndoApply => _preApplyMetadata is not null;

    /// <summary>
    /// Explicitly applies the selected template's plan to the wizard state.
    /// Steps: Snapshot → Controller.BuildPlan → Apply metadata + path suggestions.
    /// Respects IsUserEdited on file entries. Enforces PathSafety.
    /// </summary>
    private void ExecuteApplyTemplate()
    {
        if (SelectedTemplateId == OivTemplateId.None) return;

        // 1) Build snapshot DTO — controller never sees OivViewModel.
        var fileNames = CreatorFiles.Select(f => Path.GetFileName(f.SourcePath)).ToList();
        var snapshot = new OivWizardSnapshot(
            CreatorName,
            CreatorDescription,
            CreatorVersion,
            fileNames);

        // 2) Get the plan from the controller.
        var plan = _templateController.BuildPlan(SelectedTemplateId, snapshot);

        // 3) Store pre-apply snapshot for undo.
        _preApplyMetadata = new Dictionary<string, string>();
        if (plan.MetadataUpdates.ContainsKey("Description"))
            _preApplyMetadata["Description"] = CreatorDescription;
        if (plan.MetadataUpdates.ContainsKey("Version"))
            _preApplyMetadata["Version"] = CreatorVersion;

        _preApplyPaths = new Dictionary<string, string>();

        // 4) Apply metadata deltas — only keys present in plan.
        foreach (var (key, value) in plan.MetadataUpdates)
        {
            switch (key)
            {
                case "Description":
                    CreatorDescription = value;
                    break;
                case "Version":
                    CreatorVersion = value;
                    break;
                // Future: add more metadata keys here.
            }
        }

        // 5) Apply path suggestions — respect IsUserEdited, enforce PathSafety.
        int pathsApplied = 0;
        int pathsSkippedUserEdited = 0;
        int pathsSkippedUnsafe = 0;

        foreach (var file in CreatorFiles)
        {
            if (file.IsUserEdited)
            {
                pathsSkippedUserEdited++;
                continue;
            }

            var suggestedPath = GetSuggestedPath(file.SourcePath, plan, out bool wasUnsafe);
            if (wasUnsafe) pathsSkippedUnsafe++;
            if (suggestedPath is null) continue;

            // Store pre-apply path for undo.
            _preApplyPaths[file.SourcePath] = file.InstallPath;
            file.InstallPath = suggestedPath;
            pathsApplied++;
        }

        // 6) Report status.
        var parts = new List<string>();
        if (plan.MetadataUpdates.Count > 0)
            parts.Add($"{plan.MetadataUpdates.Count} metadata field(s) updated");
        if (pathsApplied > 0)
            parts.Add($"{pathsApplied} path(s) suggested");
        if (pathsSkippedUserEdited > 0)
            parts.Add($"{pathsSkippedUserEdited} user-edited path(s) kept");
        if (pathsSkippedUnsafe > 0)
            parts.Add($"{pathsSkippedUnsafe} unsafe path(s) rejected");

        TemplateApplyStatus = parts.Count > 0
            ? $"Template applied: {string.Join(", ", parts)}."
            : "Template applied (no changes).";

        OnPropertyChanged(nameof(CanUndoApply));
    }

    private string? GetSuggestedPath(string sourcePath, OivTemplateApplyPlan plan, out bool wasUnsafe)
    {
        wasUnsafe = false;
        var fileName = Path.GetFileName(sourcePath);
        var ext = Path.GetExtension(sourcePath);

        PathRule? bestRule = null;
        foreach (var rule in plan.PathSuggestions)
        {
            if (rule.MatchFileName)
            {
                if (string.Equals(fileName, rule.Match, StringComparison.OrdinalIgnoreCase))
                {
                    bestRule = rule;
                    break;
                }
            }
            else
            {
                if (string.Equals(ext, rule.Match, StringComparison.OrdinalIgnoreCase))
                    bestRule ??= rule;
            }
        }

        if (bestRule is null) return null;

        var suggestedPath = bestRule.TargetPath;
        if (suggestedPath.EndsWith('/') || suggestedPath.EndsWith('\\'))
            suggestedPath += fileName;

        try
        {
            var syntheticRoot = Path.GetTempPath();
            PathSafety.GetSafePath(syntheticRoot, suggestedPath);
            return suggestedPath;
        }
        catch (InvalidOperationException)
        {
            wasUnsafe = true;
            AppLogger.Warning($"[OIV_TEMPLATE] Unsafe path rejected: {suggestedPath}");
            return null;
        }
    }

    /// <summary>
    /// Reverts the most recent Apply operation (1-level undo).
    /// </summary>
    private void ExecuteUndoApplyTemplate()
    {
        if (_preApplyMetadata is null) return;

        // Undo metadata.
        foreach (var (key, value) in _preApplyMetadata)
        {
            switch (key)
            {
                case "Description": CreatorDescription = value; break;
                case "Version":     CreatorVersion = value;     break;
            }
        }

        // Undo paths.
        if (_preApplyPaths is not null)
        {
            foreach (var file in CreatorFiles)
            {
                if (_preApplyPaths.TryGetValue(file.SourcePath, out var oldPath))
                    file.InstallPath = oldPath;
            }
        }

        _preApplyMetadata = null;
        _preApplyPaths = null;
        TemplateApplyStatus = "Template changes reverted.";
        OnPropertyChanged(nameof(CanUndoApply));
    }

    public string CreatorOutputPath
    {
        get => _creatorOutputPath;
        set
        {
            if (SetProperty(ref _creatorOutputPath, value))
            {
                OnPropertyChanged(nameof(CanExport));
                RefreshCreatorCommands();
            }
        }
    }

    // ── Source file list ──────────────────────────────────────────────────────

    public ObservableCollection<OivFileEntry> CreatorFiles { get; } = [];

    public bool CanBuildPlan =>
        !string.IsNullOrWhiteSpace(CreatorName) &&
        CreatorFiles.Count > 0 &&
        !IsWorking;

    // ── Plan / review ─────────────────────────────────────────────────────────

    public OivPackagePlan? CreatorPlan
    {
        get => _creatorPlan;
        private set
        {
            if (SetProperty(ref _creatorPlan, value))
            {
                OnPropertyChanged(nameof(PlanHasErrors));
                OnPropertyChanged(nameof(PlanHasWarnings));
                OnPropertyChanged(nameof(PlanErrors));
                OnPropertyChanged(nameof(PlanWarnings));
                OnPropertyChanged(nameof(PlanFileCount));
                OnPropertyChanged(nameof(PlanSizeLabel));
                OnPropertyChanged(nameof(PlanFiles));
                OnPropertyChanged(nameof(CanExport));
                RefreshCreatorCommands();
            }
        }
    }

    public bool   PlanHasErrors   => (_creatorPlan?.Errors.Count   ?? 0) > 0;
    public bool   PlanHasWarnings => (_creatorPlan?.Warnings.Count ?? 0) > 0;
    public IReadOnlyList<string> PlanErrors   => _creatorPlan?.Errors   ?? [];
    public IReadOnlyList<string> PlanWarnings => _creatorPlan?.Warnings ?? [];
    public int    PlanFileCount   => _creatorPlan?.Files.Count ?? 0;
    public string PlanSizeLabel   => _creatorPlan?.TotalSizeLabel ?? "";
    public IReadOnlyList<OivPackageFile> PlanFiles => _creatorPlan?.Files ?? [];

    public bool CanExport =>
        _creatorPlan is { IsValid: true } &&
        !string.IsNullOrWhiteSpace(CreatorOutputPath) &&
        !IsWorking;

    public string ExportStatus
    {
        get => _exportStatus;
        private set => SetProperty(ref _exportStatus, value);
    }

    // ── Creator commands ──────────────────────────────────────────────────────

    public ICommand AddCreatorFileCommand    { get; }
    public ICommand RemoveCreatorFileCommand { get; }
    public ICommand BrowseCreatorOutputCommand { get; }
    public ICommand BuildPlanCommand         { get; }
    public ICommand BackToEditCommand        { get; }
    public ICommand ExportCommand            { get; }
    public ICommand ResetCreatorCommand      { get; }

    private void AddCreatorFile()
    {
        var pickedFiles = _fileDialogService.PickFiles(
            "Select Files to Include in OIV Package",
            "All Files|*.*",
            true);

        if (pickedFiles.Count == 0) return;

        OivTemplateApplyPlan? currentPlan = null;
        if (SelectedTemplateId != OivTemplateId.None)
        {
            var fileNames = CreatorFiles.Select(f => Path.GetFileName(f.SourcePath)).ToList();
            var snapshot = new OivWizardSnapshot(CreatorName, CreatorDescription, CreatorVersion, fileNames);
            currentPlan = _templateController.BuildPlan(SelectedTemplateId, snapshot);
        }

        foreach (var filePath in pickedFiles)
        {
            var installPath = Path.GetFileName(filePath);
            if (currentPlan != null)
            {
                var suggested = GetSuggestedPath(filePath, currentPlan, out _);
                if (suggested != null)
                {
                    installPath = suggested;
                }
            }

            CreatorFiles.Add(new OivFileEntry
            {
                SourcePath   = filePath,
                InstallPath  = installPath,
                Action       = OivFileAction.Add
            });
        }

        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void RemoveCreatorFile(OivFileEntry? entry)
    {
        if (entry is null) return;
        CreatorFiles.Remove(entry);
        OnPropertyChanged(nameof(CanBuildPlan));
    }

    private void BrowseCreatorOutput()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Save OIV Package As",
            Filter     = "OIV Package|*.oiv",
            DefaultExt = ".oiv",
            FileName   = string.IsNullOrWhiteSpace(CreatorName) ? "package" : CreatorName
        };

        if (dialog.ShowDialog() == true)
            CreatorOutputPath = dialog.FileName;
    }

    private void BuildPlan()
    {
        if (!CanBuildPlan) return;

        var template = new OivPackagePlan
        {
            Name        = CreatorName,
            Version     = CreatorVersion,
            Author      = CreatorAuthor,
            Description = CreatorDescription,
            Kind        = CreatorKind,
        };

        // Source scanner: walk each file entry using its SourcePath directly.
        var sourcePaths = CreatorFiles.Select(f => f.SourcePath).ToList();
        var scanned  = _scanner.Scan(sourcePaths, template);

        // Merge user-edited install paths from the UI list.
        // Last entry wins when the same SourcePath appears more than once (avoids ToDictionary crash).
        var userPaths = CreatorFiles
            .Where(f => File.Exists(f.SourcePath))
            .GroupBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().InstallPath, StringComparer.OrdinalIgnoreCase);

        var mergedFiles = scanned.Files
            .Select(f => userPaths.TryGetValue(f.SourcePath, out var ip)
                ? f with { InstallPath = ip }
                : f)
            .ToList();

        var merged = scanned with { Files = mergedFiles };

        CreatorPlan = _validator.Validate(merged);
        CreatorStep = 1;
    }

    private async Task ExportAsync()
    {
        if (!CanExport) return;

        IsWorking  = true;
        ExportStatus = "Building package…";

        try
        {
            // Re-validate immediately before building — guards against stale plan state.
            var freshPlan = _validator.Validate(CreatorPlan!);
            if (!freshPlan.IsValid)
            {
                UiDispatcher.Invoke(() =>
                {
                    CreatorPlan  = freshPlan;
                    ExportStatus = "Export blocked — plan has new validation errors. Review and try again.";
                    IsWorking    = false;
                });
                return;
            }

            var result = await _builder.BuildAsync(freshPlan, CreatorOutputPath);

            UiDispatcher.Invoke(() =>
            {
                ExportStatus = result.Success
                    ? $"Package created: {Path.GetFileName(CreatorOutputPath)} ({result.FilesWritten} file(s))"
                    : $"Export failed: {result.Error}";

                if (result.Success)
                    CreatorStep = 2;
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_EXPORT] Unexpected error", ex);
            UiDispatcher.Invoke(() => ExportStatus = $"Error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() =>
            {
                IsWorking = false;
                OnPropertyChanged(nameof(CanExport));
            });
        }
    }

    private void ResetCreator()
    {
        CreatorFiles.Clear();
        CreatorPlan       = null;
        CreatorName        = "";
        CreatorVersion     = "1.0";
        CreatorAuthor      = "";
        CreatorDescription = "";
        CreatorKind        = OivPackageKind.Basic;
        CreatorOutputPath  = "";
        ExportStatus       = "";
        CreatorStep        = 0;
    }

    // =========================================================================
    // INSTALLER (unchanged from before)
    // =========================================================================

    private string       _selectedOivPath = "";
    private OivPackage?  _parsedPackage;
    private string       _targetRoot = "";

    public string SelectedOivPath
    {
        get => _selectedOivPath;
        set => SetProperty(ref _selectedOivPath, value);
    }

    public string TargetRoot
    {
        get => _targetRoot;
        set => SetProperty(ref _targetRoot, value);
    }

    public OivPackage? ParsedPackage
    {
        get => _parsedPackage;
        private set
        {
            if (SetProperty(ref _parsedPackage, value))
            {
                OnPropertyChanged(nameof(HasParsedPackage));
                OnPropertyChanged(nameof(ParseError));
            }
        }
    }

    public bool    HasParsedPackage => _parsedPackage is { IsValid: true };
    public string? ParseError       => _parsedPackage is { IsValid: false } ? _parsedPackage.ValidationError : null;

    public ObservableCollection<OivFileEntry> PreviewEntries { get; } = [];

    public ICommand BrowseOivCommand { get; }
    public ICommand PreviewCommand   { get; }
    public ICommand InstallCommand   { get; }

    private void BrowseOiv()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select OIV Package",
            Filter = "OIV Packages|*.oiv|All Files|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        SelectedOivPath = dialog.FileName;
        PreviewEntries.Clear();
        ParsedPackage = null;
        StatusMessage = "Parsing package…";

        try
        {
            ParsedPackage = OivService.ParsePackage(SelectedOivPath);
            StatusMessage = ParsedPackage.IsValid
                ? $"Loaded: {ParsedPackage.Name} v{ParsedPackage.Version}"
                : $"Invalid: {ParsedPackage.ValidationError}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_PARSE] Unexpected parse error", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task PreviewAsync()
    {
        if (_parsedPackage is null || !_parsedPackage.IsValid) return;

        IsWorking = true;
        StatusMessage = "Calculating preview…";

        try
        {
            var root = string.IsNullOrWhiteSpace(TargetRoot)
                ? AppConfig.Instance.GtaPath
                : TargetRoot;

            var entries = await Task.Run(() => OivService.PreviewInstall(_parsedPackage, root));

            UiDispatcher.Invoke(() =>
            {
                PreviewEntries.Clear();
                foreach (var e in entries) PreviewEntries.Add(e);

                StatusMessage = $"Preview: {entries.Count(e => e.Action == OivFileAction.Add)} add, " +
                                $"{entries.Count(e => e.Action == OivFileAction.Replace)} replace";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_INSTALL] Preview error", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Preview error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsWorking = false);
        }
    }

    private async Task InstallAsync()
    {
        if (_parsedPackage is null || !_parsedPackage.IsValid) return;

        IsWorking = true;
        StatusMessage = "Installing…";

        try
        {
            var root = string.IsNullOrWhiteSpace(TargetRoot)
                ? AppConfig.Instance.GtaPath
                : TargetRoot;

            var result = await OivService.InstallPackage(_parsedPackage, root);

            UiDispatcher.Invoke(() =>
            {
                StatusMessage = result.Success
                    ? $"Installed {result.FilesWritten} file(s) successfully."
                    : $"Install failed: {result.Error}";

                if (result.Success)
                    _ = PreviewAsync();
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_INSTALL] Unexpected install error", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsWorking = false);
        }
    }

    private void RefreshCreatorCommands()
    {
        _buildPlanCommand?.RaiseCanExecuteChanged();
        _exportCommand?.RaiseCanExecuteChanged();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public OivViewModel(IFileDialogService? fileDialogService = null)
    {
        // TODO: Migrate to pure DI (no default) when VM composition is updated
        _fileDialogService = fileDialogService ?? new OpenFileDialogService();
        AddCreatorFileCommand    = new RelayCommand(AddCreatorFile);
        RemoveCreatorFileCommand = new RelayCommand<OivFileEntry>(RemoveCreatorFile);
        BrowseCreatorOutputCommand = new RelayCommand(BrowseCreatorOutput);
        _buildPlanCommand        = new RelayCommand(BuildPlan, () => CanBuildPlan);
        BuildPlanCommand         = _buildPlanCommand;
        BackToEditCommand        = new RelayCommand(() => CreatorStep = 0);
        _exportCommand           = new RelayCommand(() => _ = ExportAsync(), () => CanExport);
        ExportCommand            = _exportCommand;
        ResetCreatorCommand      = new RelayCommand(ResetCreator);
        ApplyTemplateCommand     = new RelayCommand(ExecuteApplyTemplate, () => SelectedTemplateId != OivTemplateId.None);
        UndoApplyTemplateCommand = new RelayCommand(ExecuteUndoApplyTemplate, () => CanUndoApply);

        CreatorFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanBuildPlan));
            RefreshCreatorCommands();
        };

        BrowseOivCommand = new RelayCommand(BrowseOiv);
        PreviewCommand   = new RelayCommand(
            () => _ = PreviewAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
        InstallCommand   = new RelayCommand(
            () => _ = InstallAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
    }
}
