using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly BackupService _backup = new();
    private readonly ExportService _export = new();
    private readonly BatchReinstallService _reinstall = new();

    private string _gtaPath = AppConfig.Instance.GtaPath;
    private string _backupPath = AppConfig.Instance.BackupPath;
    private bool _autoBackup = AppConfig.Instance.AutoBackupOnInstall;
    private bool _confirmUninstall = AppConfig.Instance.ConfirmBeforeUninstall;
    private bool _autoLaunch = AppConfig.Instance.AutoLaunchAfterInstall;
    private string _statusMessage = "";
    private bool _isBusy;

    public ObservableCollection<string> ProgressLog { get; } = [];

    /// <summary>Live status service — exposed so the view can bind to it.</summary>
    public LspdfrStatusService LspdfrStatus { get; } = LspdfrStatusService.Instance;

    public string GtaPath
    {
        get => _gtaPath;
        set
        {
            SetProperty(ref _gtaPath, value);
            AppConfig.Instance.GtaPath = value;
            LspdfrStatusService.Instance.Refresh();
        }
    }

    public string BackupPath
    {
        get => _backupPath;
        set { SetProperty(ref _backupPath, value); AppConfig.Instance.BackupPath = value; }
    }

    public bool AutoBackupOnInstall
    {
        get => _autoBackup;
        set { SetProperty(ref _autoBackup, value); AppConfig.Instance.AutoBackupOnInstall = value; }
    }

    public bool ConfirmBeforeUninstall
    {
        get => _confirmUninstall;
        set { SetProperty(ref _confirmUninstall, value); AppConfig.Instance.ConfirmBeforeUninstall = value; }
    }

    public bool AutoLaunchAfterInstall
    {
        get => _autoLaunch;
        set { SetProperty(ref _autoLaunch, value); AppConfig.Instance.AutoLaunchAfterInstall = value; }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isBusy;

    public string? LastBackupDate =>
        AppConfig.Instance.LastBackupDate?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public ICommand BrowseGtaPathCommand { get; }
    public ICommand BrowseBackupPathCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand ExportManifestCommand { get; }
    public ICommand ImportManifestCommand { get; }
    public ICommand OpenLogFolderCommand { get; }

    public SettingsViewModel()
    {
        BrowseGtaPathCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select GTA V installation folder",
                InitialDirectory = GtaPath,
            };
            if (dlg.ShowDialog() == true)
                GtaPath = dlg.FolderName;
        });

        BrowseBackupPathCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select backup folder",
                InitialDirectory = BackupPath,
            };
            if (dlg.ShowDialog() == true)
                BackupPath = dlg.FolderName;
        });

        SaveSettingsCommand = new RelayCommand(() =>
        {
            AppConfig.Instance.Save();
            LspdfrStatusService.Instance.Refresh();
            StatusMessage = "Settings saved.";
        });

        CreateBackupCommand = new RelayCommand(
            () => _ = RunAsync(() => _backup.CreateBackupAsync(Progress())),
            () => IsIdle);

        RestoreBackupCommand = new RelayCommand(
            () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Backup File",
                    Filter = "Backup Files|*.zip|All Files|*.*",
                    InitialDirectory = BackupPath,
                };
                if (dlg.ShowDialog() == true)
                    _ = RunAsync(() => _backup.RestoreFromBackupAsync(dlg.FileName, Progress()));
            },
            () => IsIdle);

        ExportManifestCommand = new RelayCommand(
            () =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Mod Manifest",
                    Filter = "Manifest|*.lspmanifest|ZIP Package|*.zip",
                    FileName = $"lsp_manifest_{DateTime.Now:yyyyMMdd}",
                };
                if (dlg.ShowDialog() != true) return;
                var includeArchives = dlg.FilterIndex == 2;
                _ = RunAsync(() => _export.ExportAsync(dlg.FileName, includeArchives, Progress()));
            },
            () => IsIdle);

        ImportManifestCommand = new RelayCommand(
            () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Mod Manifest",
                    Filter = "Manifest|*.lspmanifest;*.json|ZIP Package|*.zip|All Files|*.*",
                };
                if (dlg.ShowDialog() == true)
                    _ = RunAsync(() => _reinstall.ReinstallFromManifestAsync(dlg.FileName, Progress()));
            },
            () => IsIdle);

        OpenLogFolderCommand = new RelayCommand(() =>
        {
            var logDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LSPDFRManager");
            if (System.IO.Directory.Exists(logDir))
                System.Diagnostics.Process.Start("explorer.exe", logDir);
        });
    }

    private IProgress<string> Progress() => new Progress<string>(msg =>
    {
        ProgressLog.Add(msg);
        StatusMessage = msg;
    });

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ProgressLog.Clear();
        try { await action(); }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; ProgressLog.Add($"Error: {ex.Message}"); }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(LastBackupDate));
        }
    }
}
