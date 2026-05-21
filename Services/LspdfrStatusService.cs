using System.ComponentModel;
using System.Diagnostics;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Detects the live state of GTA V, LSPDFR, and ScriptHookV by inspecting
/// the configured GTA V directory. Call <see cref="Refresh"/> whenever the
/// GTA path changes so bindings update.
/// </summary>
public sealed class LspdfrStatusService : INotifyPropertyChanged
{
    private static LspdfrStatusService? _instance;
    public static LspdfrStatusService Instance => _instance ??= new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Validity checks ──────────────────────────────────────────────

    /// <summary>A known GTA executable exists at the configured path.</summary>
    public bool IsGtaPathValid =>
        LspdfrInstallLocator.IsGtaInstalled(AppConfig.Instance.GtaPath);

    /// <summary>LSPDFR core DLL, folder, or tool is present.</summary>
    public bool IsLspdfrInstalled =>
        IsGtaPathValid &&
        LspdfrInstallLocator.IsLspdfrInstalled(AppConfig.Instance.GtaPath);

    /// <summary><c>ScriptHookV.dll</c> is present.</summary>
    public bool IsScriptHookVInstalled =>
        IsGtaPathValid &&
        File.Exists(Path.Combine(AppConfig.Instance.GtaPath, "ScriptHookV.dll"));

    // ── Version strings ──────────────────────────────────────────────

    /// <summary>File version of the detected GTA executable, or "–" if not found.</summary>
    public string GtaVersion
    {
        get
        {
            var exe = LspdfrInstallLocator.FindGtaExe(AppConfig.Instance.GtaPath);
            if (exe is null) return "–";
            return FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "–";
        }
    }

    /// <summary>File version of the detected LSPDFR core DLL, or "–" if not found.</summary>
    public string LspdfrVersion
    {
        get
        {
            var dll = LspdfrInstallLocator.FindLspdfrCore(AppConfig.Instance.GtaPath);
            if (dll is null) return "–";
            return FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "–";
        }
    }

    /// <summary>File version of <c>ScriptHookV.dll</c>, or "–" if not found.</summary>
    public string ScriptHookVVersion
    {
        get
        {
            var dll = Path.Combine(AppConfig.Instance.GtaPath, "ScriptHookV.dll");
            if (!File.Exists(dll)) return "–";
            return FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "–";
        }
    }

    // ── Convenience ──────────────────────────────────────────────────

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for all status properties so WPF
    /// bindings refresh after the GTA path is changed or saved.
    /// </summary>
    public void Refresh()
    {
        Notify(nameof(IsGtaPathValid));
        Notify(nameof(IsLspdfrInstalled));
        Notify(nameof(IsScriptHookVInstalled));
        Notify(nameof(GtaVersion));
        Notify(nameof(LspdfrVersion));
        Notify(nameof(ScriptHookVVersion));
    }
}
