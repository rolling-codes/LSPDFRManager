using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class DashboardViewModel : ObservableObject
{
    public DashboardStatusService Status { get; } = DashboardStatusService.Instance;
    public BrowseApiServiceManager ApiManager { get; } = BrowseApiServiceManager.Instance;

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ScanPluginsCommand { get; }
    public ICommand AnalyzeCrashLogsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand ApplySafeLaunchCommand { get; }
    public ICommand OpenGtaFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand LaunchGtaCommand { get; }
    public ICommand LaunchRphCommand { get; }
    public ICommand RefreshCommand { get; }

    public CompatibilityViewModel Compatibility { get; } = new();
    public PatrolReadinessViewModel Readiness { get; } = new();

    public IEnumerable<ComponentRow> CompatibilityRows =>
    [
        Compatibility.GtaRow,
        Compatibility.LspdfrRow,
        Compatibility.RphRow,
        Compatibility.ShvRow,
        Compatibility.ShvdnRow,
    ];

    public DashboardViewModel()
    {
        ScanPluginsCommand = new RelayCommand(() => _ = RunScanAsync());
        AnalyzeCrashLogsCommand = new RelayCommand(AnalyzeCrash);
        CreateBackupCommand = new RelayCommand(() => _ = RunBackupAsync());
        ApplySafeLaunchCommand = new RelayCommand(() => _ = RunSafeLaunchAsync());
        OpenGtaFolderCommand = new RelayCommand(OpenGtaFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        LaunchGtaCommand = new RelayCommand(LaunchGta);
        LaunchRphCommand = new RelayCommand(LaunchRph);
        RefreshCommand = new RelayCommand(() => _ = RefreshDashboardAsync());

        Compatibility.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CompatibilityRows));

        _ = Compatibility.RefreshAsync();
        _ = Readiness.CheckAsync();
    }

    private async Task RunScanAsync()
    {
        StatusMessage = "Scanning plugins…";
        var scanner = new PluginHealthScanner();
        var results = await Task.Run(scanner.Scan);
        StatusMessage = $"Scan complete — {results.Count} finding(s).";
        Status.Refresh();
    }

    private void AnalyzeCrash()
    {
        var analyzer = new CrashLogAnalyzer();
        var findings = analyzer.AnalyzeAll();
        StatusMessage = findings.Count > 0
            ? $"Found {findings.Count} crash indicator(s). Check Diagnostics tab."
            : "No crash indicators found.";
    }

    private async Task RunBackupAsync()
    {
        StatusMessage = "Creating backup…";
        await BackupScheduler.Instance.CreateBackupAsync();
        Status.Refresh();
        StatusMessage = "Backup created.";
    }

    private async Task RunSafeLaunchAsync()
    {
        StatusMessage = "Applying Safe Launch (LSPDFR Only)…";
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");
        await new SafeLaunchManager().ApplyAsync(plan);
        Status.Refresh();
        StatusMessage = "Safe Launch applied. Restart the game.";
    }

    private void OpenGtaFolder()
    {
        var path = AppConfig.Instance.GtaPath;
        StartShellProcess("explorer.exe", path, "Could not open GTA V folder.");
    }

    private async Task RefreshDashboardAsync()
    {
        Status.Refresh();
        await Compatibility.RefreshAsync();
        await Readiness.CheckAsync();
        OnPropertyChanged(nameof(CompatibilityRows));
        StatusMessage = "Status refreshed.";
    }

    private void OpenLogsFolder()
    {
        StartShellProcess("explorer.exe", AppDataPaths.Root, "Could not open logs folder.");
    }

    private void LaunchGta()
    {
        var exe = LspdfrInstallLocator.FindGtaExe(AppConfig.Instance.GtaPath);
        if (exe is not null)
            StartShellProcess(exe, AppConfig.Instance.GtaPath, "Could not launch GTA V.");
        else
            StatusMessage = "GTA executable was not found.";
    }

    private void LaunchRph()
    {
        var exe = LspdfrInstallLocator.FindRagePluginHook(AppConfig.Instance.GtaPath);
        if (exe is not null)
            StartShellProcess(exe, AppConfig.Instance.GtaPath, "Could not launch RAGE Plugin Hook.");
        else
            StatusMessage = "RAGEPluginHook.exe was not found.";
    }

    private void StartShellProcess(string fileName, string workingDirectoryOrArgument, string failureMessage)
    {
        try
        {
            if (string.Equals(fileName, "explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(workingDirectoryOrArgument))
                {
                    StatusMessage = "Folder was not found.";
                    return;
                }

                Process.Start(fileName, workingDirectoryOrArgument);
                StatusMessage = "Folder opened.";
                return;
            }

            Process.Start(new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectoryOrArgument,
            });
            StatusMessage = "Launch requested.";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[Dashboard] {failureMessage}", ex);
            StatusMessage = $"{failureMessage} {ex.Message}";
        }
    }
}
