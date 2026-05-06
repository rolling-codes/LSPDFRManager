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

    public SettingsViewModel()
    {
        BrowseGtaPathCommand = new RelayCommand(BrowseForGtaPath);
        BrowseBackupPathCommand = new RelayCommand(BrowseForBackupPath);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        CreateBackupCommand = new RelayCommand(() => _ = RunAsync(() => _backup.CreateBackupAsync(CreateProgress())), () => IsIdle);
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => IsIdle);
        ExportManifestCommand = new RelayCommand(ExportManifest, () => IsIdle);
        ImportManifestCommand = new RelayCommand(ImportManifest, () => IsIdle);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
    }

    public ObservableCollection<string> ProgressLog { get; } = [];
    public LspdfrStatusService LspdfrStatus { get; } = LspdfrStatusService.Instance;

    public string GtaPath
    {
        get => _gtaPath;
        set
        {
            if (!SetProperty(ref _gtaPath, value))
                return;

            AppConfig.Instance.GtaPath = value;
            LspdfrStatus.Refresh();
        }
    }

    public string BackupPath
    {
        get => _backupPath;
        set
        {
            if (!SetProperty(ref _backupPath, value))
                return;

            AppConfig.Instance.BackupPath = value;
        }
    }

    public bool AutoBackupOnInstall
    {
        get => _autoBackup;
        set
        {
            if (!SetProperty(ref _autoBackup, value))
                return;

            AppConfig.Instance.AutoBackupOnInstall = value;
        }
    }

    public bool ConfirmBeforeUninstall
    {
        get => _confirmUninstall;
        set
        {
            if (!SetProperty(ref _confirmUninstall, value))
                return;

            AppConfig.Instance.ConfirmBeforeUninstall = value;
        }
    }

    public bool AutoLaunchAfterInstall
    {
        get => _autoLaunch;
        set
        {
            if (!SetProperty(ref _autoLaunch, value))
                return;

            AppConfig.Instance.AutoLaunchAfterInstall = value;
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsBusy;

    public string LastBackupDate =>
        AppConfig.Instance.LastBackupDate?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public ICommand BrowseGtaPathCommand { get; }
    public ICommand BrowseBackupPathCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand ExportManifestCommand { get; }
    public ICommand ImportManifestCommand { get; }
    public ICommand OpenLogFolderCommand { get; }

    private void BrowseForGtaPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select GTA V installation folder",
            InitialDirectory = GtaPath,
        };

        if (dialog.ShowDialog() == true)
            GtaPath = dialog.FolderName;
    }

    private void BrowseForBackupPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select backup folder",
            InitialDirectory = BackupPath,
        };

        if (dialog.ShowDialog() == true)
            BackupPath = dialog.FolderName;
    }

    private void SaveSettings()
    {
        AppConfig.Instance.Save();
        LspdfrStatus.Refresh();
        StatusMessage = "Settings saved.";
    }

    private void RestoreBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Backup File",
            Filter = "Backup Files|*.zip|All Files|*.*",
            InitialDirectory = BackupPath,
        };

        if (dialog.ShowDialog() == true)
            _ = RunAsync(() => _backup.RestoreFromBackupAsync(dialog.FileName, CreateProgress()));
    }

    private void ExportManifest()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Mod Manifest",
            Filter = "Manifest|*.lspmanifest|ZIP Package|*.zip",
            FileName = $"lsp_manifest_{DateTime.Now:yyyyMMdd}",
        };

        if (dialog.ShowDialog() != true)
            return;

        var includeArchives = dialog.FilterIndex == 2;
        _ = RunAsync(() => _export.ExportAsync(dialog.FileName, includeArchives, CreateProgress()));
    }

    private void ImportManifest()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Mod Manifest",
            Filter = "Manifest|*.lspmanifest;*.json|ZIP Package|*.zip|All Files|*.*",
        };

        if (dialog.ShowDialog() == true)
            _ = RunAsync(() => _reinstall.ReinstallFromManifestAsync(dialog.FileName, CreateProgress()));
    }

    private void OpenLogFolder()
    {
        if (Directory.Exists(AppDataPaths.Root))
            System.Diagnostics.Process.Start("explorer.exe", AppDataPaths.Root);
    }

    private IProgress<string> CreateProgress() => new Progress<string>(message =>
    {
        ProgressLog.Add(message);
        StatusMessage = message;
    });

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ProgressLog.Clear();

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ProgressLog.Add(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(LastBackupDate));
        }
    }
}
