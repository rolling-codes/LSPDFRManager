using System.Diagnostics;
using System.Windows.Input;
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
        RefreshCommand = new RelayCommand(() => Status.Refresh());
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

    private static void OpenGtaFolder()
    {
        var path = AppConfig.Instance.GtaPath;
        if (Directory.Exists(path))
            Process.Start("explorer.exe", path);
    }

    private static void OpenLogsFolder()
    {
        if (Directory.Exists(AppDataPaths.Root))
            Process.Start("explorer.exe", AppDataPaths.Root);
    }

    private static void LaunchGta()
    {
        var exe = Path.Combine(AppConfig.Instance.GtaPath, "GTA5.exe");
        if (File.Exists(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = AppConfig.Instance.GtaPath });
    }

    private static void LaunchRph()
    {
        var exe = Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe");
        if (File.Exists(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = AppConfig.Instance.GtaPath });
    }
}
