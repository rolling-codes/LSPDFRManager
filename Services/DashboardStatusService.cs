using System.ComponentModel;
using System.Diagnostics;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class DashboardStatusService : INotifyPropertyChanged
{
    private static DashboardStatusService? _instance;
    public static DashboardStatusService Instance => _instance ??= new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public bool IsGtaPathValid =>
        File.Exists(Path.Combine(AppConfig.Instance.GtaPath, "GTA5.exe"));

    public bool IsLspdfrInstalled =>
        IsGtaPathValid &&
        File.Exists(Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe"));

    public bool IsRagePluginHookInstalled =>
        IsGtaPathValid &&
        File.Exists(Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe"));

    public int EnabledModCount =>
        ModLibraryService.Instance.Mods.Count(m => m.IsEnabled);

    public int DisabledModCount =>
        ModLibraryService.Instance.Mods.Count(m => !m.IsEnabled);

    public string GtaPath => AppConfig.Instance.GtaPath;

    public string? ActiveProfileId => AppConfig.Instance.ActiveProfileId;

    public string LastBackupDisplay =>
        AppConfig.Instance.LastBackupDate?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public string LastDiagnosticsDisplay =>
        AppConfig.Instance.LastDiagnosticsScanUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never";

    public string StorageUsed
    {
        get
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (!Directory.Exists(gtaPath)) return "–";
            try
            {
                var pluginsDir = Path.Combine(gtaPath, "plugins");
                if (!Directory.Exists(pluginsDir)) return "0 MB";
                var bytes = Directory.EnumerateFiles(pluginsDir, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
                return $"{bytes / (1024.0 * 1024):F1} MB";
            }
            catch { return "–"; }
        }
    }

    public string? LastCrashDetected
    {
        get
        {
            var logPath = Path.Combine(AppConfig.Instance.GtaPath, "RagePluginHook.log");
            if (!File.Exists(logPath)) return null;
            try
            {
                var info = new FileInfo(logPath);
                return info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
            }
            catch { return null; }
        }
    }

    public void Refresh()
    {
        Notify(nameof(IsGtaPathValid));
        Notify(nameof(IsLspdfrInstalled));
        Notify(nameof(IsRagePluginHookInstalled));
        Notify(nameof(EnabledModCount));
        Notify(nameof(DisabledModCount));
        Notify(nameof(GtaPath));
        Notify(nameof(ActiveProfileId));
        Notify(nameof(LastBackupDisplay));
        Notify(nameof(LastDiagnosticsDisplay));
        Notify(nameof(StorageUsed));
        Notify(nameof(LastCrashDetected));
    }
}
