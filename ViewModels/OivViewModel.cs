using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class OivViewModel : ObservableObject
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly IOivSourceScanner    _scanner   = new OivSourceScanner();
    private readonly IOivPackageValidator _validator = new OivPackageValidator();
    private readonly IOivPackageBuilder   _builder   = new OivPackageBuilder();

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
                OnPropertyChanged(nameof(CanBuildPlan));
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

    public string CreatorOutputPath
    {
        get => _creatorOutputPath;
        set
        {
            if (SetProperty(ref _creatorOutputPath, value))
                OnPropertyChanged(nameof(CanExport));
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
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public bool   PlanHasErrors   => _creatorPlan?.Errors.Count > 0;
    public bool   PlanHasWarnings => _creatorPlan?.Warnings.Count > 0;
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
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Files to Include in OIV Package",
            Filter = "All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        foreach (var filePath in dialog.FileNames)
        {
            CreatorFiles.Add(new OivFileEntry
            {
                SourcePath   = filePath,
                InstallPath  = Path.GetFileName(filePath),
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
        // The scanner sets InstallPath = filename; the user may have changed it.
        var userPaths = CreatorFiles
            .Where(f => File.Exists(f.SourcePath))
            .ToDictionary(f => f.SourcePath, f => f.InstallPath, StringComparer.OrdinalIgnoreCase);

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
            var result = await _builder.BuildAsync(CreatorPlan!, CreatorOutputPath);

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

    // ── Constructor ───────────────────────────────────────────────────────────

    public OivViewModel()
    {
        AddCreatorFileCommand    = new RelayCommand(AddCreatorFile);
        RemoveCreatorFileCommand = new RelayCommand<OivFileEntry>(RemoveCreatorFile);
        BrowseCreatorOutputCommand = new RelayCommand(BrowseCreatorOutput);
        BuildPlanCommand         = new RelayCommand(BuildPlan, () => CanBuildPlan);
        BackToEditCommand        = new RelayCommand(() => CreatorStep = 0);
        ExportCommand            = new RelayCommand(() => _ = ExportAsync(), () => CanExport);
        ResetCreatorCommand      = new RelayCommand(ResetCreator);

        BrowseOivCommand = new RelayCommand(BrowseOiv);
        PreviewCommand   = new RelayCommand(
            () => _ = PreviewAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
        InstallCommand   = new RelayCommand(
            () => _ = InstallAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
    }
}
