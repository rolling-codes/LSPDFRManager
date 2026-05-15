using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class BackupsViewModel : ObservableObject
{
    private readonly BackupScheduler _scheduler = BackupScheduler.Instance;
    private readonly RestorePointService _restorePoints = RestorePointService.Instance;
    private readonly BackupService _backupService = new();
    private bool _isBusy;
    private string _statusMessage = "";
    private RestorePoint? _selectedRestorePoint;

    // ── EUP Backup Easy Editor state ──────────────────────────────────────────
    private BackupEasyEditorService? _editor;

    private string _selectedEupDepartment = "Any";
    private string _selectedEupCounty = "Any";
    private string _selectedEupGender = "Any";
    private string _selectedBackupDepartment = "Any";
    private string _selectedBackupCounty = "Any";
    private string _selectedBackupCategory = "Any";
    private EupUniformDefinition? _selectedEupUniform;
    private BackupUnitDefinition? _selectedBackupUnit;
    private BackupUniformPatchPreview? _currentPreview;
    private string _eupStatusMessage = "";

    public ObservableCollection<BackupManifest> Backups { get; } = [];
    public ObservableCollection<RestorePoint> RestorePoints { get; } = [];
    public ObservableCollection<string> ProgressLog { get; } = [];

    // EUP filter options
    public ObservableCollection<string> EupDepartments { get; } =
        ["Any", "LSPD", "LSSD", "BCSO", "SAHP", "FIB", "SWAT", "Park Ranger", "Fire/EMS", "Unknown"];
    public ObservableCollection<string> EupCounties { get; } =
        ["Any", "Los Santos", "Blaine County", "Statewide", "Unknown"];
    public ObservableCollection<string> EupGenders { get; } =
        ["Any", "Male", "Female", "Unknown"];
    public ObservableCollection<string> BackupDepartments { get; } =
        ["Any", "LSPD", "LSSD", "BCSO", "SAHP", "FIB", "SWAT", "Unknown"];
    public ObservableCollection<string> BackupCounties { get; } =
        ["Any", "Los Santos", "Blaine County", "Statewide", "Unknown"];
    public ObservableCollection<string> BackupCategories { get; } =
        ["Any", "LocalPatrol", "StatePatrol", "Sheriff", "Supervisor",
         "SWAT", "Detective", "K9", "AirSupport", "Unknown"];

    public ObservableCollection<EupUniformDefinition> EupUniforms { get; } = [];
    public ObservableCollection<BackupUnitDefinition> BackupUnits { get; } = [];
    public ObservableCollection<string> MismatchWarnings { get; } = [];

    public string SelectedEupDepartment
    {
        get => _selectedEupDepartment;
        set { if (SetProperty(ref _selectedEupDepartment, value)) RefreshEupUniforms(); }
    }
    public string SelectedEupCounty
    {
        get => _selectedEupCounty;
        set { if (SetProperty(ref _selectedEupCounty, value)) RefreshEupUniforms(); }
    }
    public string SelectedEupGender
    {
        get => _selectedEupGender;
        set { if (SetProperty(ref _selectedEupGender, value)) RefreshEupUniforms(); }
    }
    public string SelectedBackupDepartment
    {
        get => _selectedBackupDepartment;
        set { if (SetProperty(ref _selectedBackupDepartment, value)) RefreshBackupUnits(); }
    }
    public string SelectedBackupCounty
    {
        get => _selectedBackupCounty;
        set { if (SetProperty(ref _selectedBackupCounty, value)) RefreshBackupUnits(); }
    }
    public string SelectedBackupCategory
    {
        get => _selectedBackupCategory;
        set { if (SetProperty(ref _selectedBackupCategory, value)) RefreshBackupUnits(); }
    }

    public EupUniformDefinition? SelectedEupUniform
    {
        get => _selectedEupUniform;
        set
        {
            if (SetProperty(ref _selectedEupUniform, value))
                OnPropertyChanged(nameof(CanPreview));
        }
    }
    public BackupUnitDefinition? SelectedBackupUnit
    {
        get => _selectedBackupUnit;
        set
        {
            if (SetProperty(ref _selectedBackupUnit, value))
                OnPropertyChanged(nameof(CanPreview));
        }
    }

    public bool CanPreview => SelectedEupUniform != null && SelectedBackupUnit != null;
    public bool CanApply => _currentPreview?.CanApply == true;
    public bool CanRevert => _currentPreview?.SourceFile is { Length: > 0 } sf &&
                             HasTimestampedBackups(sf);
    public bool HasMismatches => MismatchWarnings.Count > 0;
    public string PreviewBefore => string.Join("\n", _currentPreview?.BeforeLines ?? []);
    public string PreviewAfter => string.Join("\n", _currentPreview?.AfterLines ?? []);
    public string ConfidenceLabel => _currentPreview == null ? "" :
        $"Confidence: {_currentPreview.Confidence:P0}" +
        (_currentPreview.IsReadOnlyPreview ? " — Read-only preview" : "");

    public string EupStatusMessage
    {
        get => _eupStatusMessage;
        set => SetProperty(ref _eupStatusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsIdle => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RestorePoint? SelectedRestorePoint
    {
        get => _selectedRestorePoint;
        set => SetProperty(ref _selectedRestorePoint, value);
    }

    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand RestorePointCommand { get; }
    public ICommand DeleteRestorePointCommand { get; }

    public ICommand RefreshEupUniformsCommand { get; }
    public ICommand RefreshBackupUnitsCommand { get; }
    public ICommand PreviewAssignmentCommand { get; }
    public ICommand ApplyAssignmentCommand { get; }
    public ICommand RevertAssignmentCommand { get; }

    public BackupsViewModel()
    {
        CreateBackupCommand = new RelayCommand(() => _ = CreateBackupAsync(), () => IsIdle);
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => IsIdle);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        RestorePointCommand = new RelayCommand(() => _ = RestorePointAsync(), () => SelectedRestorePoint != null && IsIdle);
        DeleteRestorePointCommand = new RelayCommand(() => _ = DeleteRestorePointAsync(), () => SelectedRestorePoint != null);

        RefreshEupUniformsCommand = new RelayCommand(RefreshEupUniforms);
        RefreshBackupUnitsCommand = new RelayCommand(RefreshBackupUnits);
        PreviewAssignmentCommand = new RelayCommand(DoPreviewAssignment, () => CanPreview);
        ApplyAssignmentCommand = new RelayCommand(DoApplyAssignment, () => CanApply);
        RevertAssignmentCommand = new RelayCommand(DoRevertAssignment, () => CanRevert);

        _scheduler.LoadManifests();
        _restorePoints.Load();
        Reload();
    }

    private void Reload()
    {
        Backups.Clear();
        foreach (var b in _scheduler.Manifests) Backups.Add(b);

        RestorePoints.Clear();
        foreach (var rp in _restorePoints.Points) RestorePoints.Add(rp);
    }

    private async Task CreateBackupAsync()
    {
        IsBusy = true;
        ProgressLog.Clear();
        var progress = new Progress<string>(m => Core.UiDispatcher.Invoke(() => { ProgressLog.Add(m); StatusMessage = m; }));
        try { await _scheduler.CreateBackupAsync(progress); Reload(); StatusMessage = "Backup created."; }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void RestoreBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select Backup", Filter = "Backup Files|*.zip|All|*.*", InitialDirectory = AppConfig.Instance.BackupPath };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true;
        var progress = new Progress<string>(m => Core.UiDispatcher.Invoke(() => { ProgressLog.Add(m); StatusMessage = m; }));
        _ = _backupService.RestoreFromBackupAsync(dialog.FileName, progress)
            .ContinueWith(_ => Core.UiDispatcher.Invoke(() => IsBusy = false));
    }

    private async Task RestorePointAsync()
    {
        if (SelectedRestorePoint is null) return;
        IsBusy = true;
        var progress = new Progress<string>(m => Core.UiDispatcher.Invoke(() => { ProgressLog.Add(m); StatusMessage = m; }));
        try { await _restorePoints.RestoreAsync(SelectedRestorePoint, progress); StatusMessage = "Restore point applied."; }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task DeleteRestorePointAsync()
    {
        if (SelectedRestorePoint is null) return;
        await _restorePoints.DeleteAsync(SelectedRestorePoint);
        Reload();
        StatusMessage = "Restore point deleted.";
    }

    private static void OpenBackupFolder()
    {
        var path = AppConfig.Instance.BackupPath;
        if (Directory.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", path);
    }

    // ── EUP Backup Easy Editor actions ────────────────────────────────────────

    private void RefreshEupUniforms()
    {
        EupUniforms.Clear();
        var gender = _selectedEupGender switch
        {
            "Male"    => (EupGender?)EupGender.Male,
            "Female"  => (EupGender?)EupGender.Female,
            "Unknown" => (EupGender?)EupGender.Unknown,
            _         => null,
        };
        foreach (var u in GetEditor().GetEupUniforms(
            IsAny(_selectedEupDepartment) ? null : _selectedEupDepartment,
            IsAny(_selectedEupCounty) ? null : _selectedEupCounty,
            gender))
            EupUniforms.Add(u);
        EupStatusMessage = $"{EupUniforms.Count} uniform(s) discovered.";
    }

    private void RefreshBackupUnits()
    {
        BackupUnits.Clear();
        foreach (var u in GetEditor().GetBackupUnits(
            IsAny(_selectedBackupDepartment) ? null : _selectedBackupDepartment,
            IsAny(_selectedBackupCounty) ? null : _selectedBackupCounty,
            null,
            IsAny(_selectedBackupCategory) ? null : _selectedBackupCategory))
            BackupUnits.Add(u);
    }

    private void DoPreviewAssignment()
    {
        if (SelectedEupUniform is null || SelectedBackupUnit is null) return;

        MismatchWarnings.Clear();
        _currentPreview = null;
        string? xmlFile;
        try
        {
            var files = new BackupConfigDiscoveryService(AppConfig.Instance.GtaPath ?? "")
                .DiscoverBackupXmlFiles();
            xmlFile = files.FirstOrDefault(f =>
                BackupXmlParser.Parse(f).Any(u =>
                    u.Agency.Equals(SelectedBackupUnit.Agency, StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            EupStatusMessage = $"Unable to preview assignment safely: {ex.Message}";
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanRevert));
            OnPropertyChanged(nameof(HasMismatches));
            OnPropertyChanged(nameof(PreviewBefore));
            OnPropertyChanged(nameof(PreviewAfter));
            OnPropertyChanged(nameof(ConfidenceLabel));
            return;
        }

        if (xmlFile is null)
        {
            EupStatusMessage = $"No backup XML found containing agency '{SelectedBackupUnit.Agency}'.";
            return;
        }

        _currentPreview = BackupEasyEditorService.PreviewAssignment(
            SelectedEupUniform, SelectedBackupUnit, xmlFile);

        foreach (var w in _currentPreview.MismatchWarnings)
            MismatchWarnings.Add(w);

        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanRevert));
        OnPropertyChanged(nameof(HasMismatches));
        OnPropertyChanged(nameof(PreviewBefore));
        OnPropertyChanged(nameof(PreviewAfter));
        OnPropertyChanged(nameof(ConfidenceLabel));

        EupStatusMessage = _currentPreview.CanApply
            ? "Preview ready. Review all warnings before applying."
            : "Cannot apply: " + string.Join("; ",
                [.. _currentPreview.MismatchWarnings, .. _currentPreview.Warnings]);
    }

    private void DoApplyAssignment()
    {
        if (_currentPreview is null) return;
        var (applied, bakPath, error) = BackupEasyEditorService.ApplyAssignment(_currentPreview);
        if (applied)
        {
            EupStatusMessage = $"Applied. Backup: {Path.GetFileName(bakPath)}.";
        }
        else
        {
            EupStatusMessage = $"Apply failed: {error}";
        }
        OnPropertyChanged(nameof(CanRevert));
    }

    private void DoRevertAssignment()
    {
        if (_currentPreview?.SourceFile is not { Length: > 0 } srcFile) return;
        var dir = Path.GetDirectoryName(srcFile) ?? "";
        var fileName = Path.GetFileName(srcFile);
        var backups = Directory.GetFiles(dir, fileName + ".bak.*")
            .OrderByDescending(f => f)
            .ToArray();

        if (backups.Length == 0)
        {
            EupStatusMessage = "No timestamped backup found.";
            return;
        }

        try
        {
            File.Copy(backups[0], srcFile, overwrite: true);
            EupStatusMessage = $"Reverted from {Path.GetFileName(backups[0])}.";
            OnPropertyChanged(nameof(CanRevert));
        }
        catch (Exception ex)
        {
            EupStatusMessage = $"Revert failed: {ex.Message}";
        }
    }

    private static bool IsAny(string value) =>
        string.IsNullOrEmpty(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase);

    private BackupEasyEditorService GetEditor()
    {
        _editor ??= new BackupEasyEditorService(AppConfig.Instance.GtaPath ?? "");
        return _editor;
    }

    private static bool HasTimestampedBackups(string sourceFile)
    {
        try
        {
            var dir = Path.GetDirectoryName(sourceFile);
            var fileName = Path.GetFileName(sourceFile);
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(dir))
                return false;

            return Directory.GetFiles(dir, fileName + ".bak.*").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
