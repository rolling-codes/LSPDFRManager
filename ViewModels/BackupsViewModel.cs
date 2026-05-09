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

    public ObservableCollection<BackupManifest> Backups { get; } = [];
    public ObservableCollection<RestorePoint> RestorePoints { get; } = [];
    public ObservableCollection<string> ProgressLog { get; } = [];

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

    public BackupsViewModel()
    {
        CreateBackupCommand = new RelayCommand(() => _ = CreateBackupAsync(), () => IsIdle);
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => IsIdle);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        RestorePointCommand = new RelayCommand(() => _ = RestorePointAsync(), () => SelectedRestorePoint != null && IsIdle);
        DeleteRestorePointCommand = new RelayCommand(() => _ = DeleteRestorePointAsync(), () => SelectedRestorePoint != null);

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
}
