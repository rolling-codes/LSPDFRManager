using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class PreLaunchChecklistService
{
    public List<PreLaunchCheckResult> Run(bool requireLspdfr = true)
    {
        var results = new List<PreLaunchCheckResult>();
        var gtaPath = AppConfig.Instance.GtaPath;

        Check(results, "GTA V path configured", !string.IsNullOrWhiteSpace(gtaPath), "Set GTA V path in Settings.", true);
        Check(results, "GTA V folder exists", Directory.Exists(gtaPath), "GTA V folder not found.", true);
        Check(results, "GTA5.exe exists", File.Exists(Path.Combine(gtaPath, "GTA5.exe")), "GTA5.exe missing.", true);

        if (requireLspdfr)
        {
            Check(results, "RAGEPluginHook.exe exists", File.Exists(Path.Combine(gtaPath, "RAGEPluginHook.exe")), "RAGEPluginHook.exe not found.", false);
            Check(results, "LSPDFR.dll exists", File.Exists(Path.Combine(gtaPath, "plugins", "LSPDFR.dll")), "LSPDFR.dll not found.", false);
        }

        Check(results, "ScriptHookV.dll exists", File.Exists(Path.Combine(gtaPath, "ScriptHookV.dll")), "ScriptHookV.dll not found (needed for scripted mods).", false);

        // Disk space check
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(gtaPath) ?? "C:");
            var enoughSpace = drive.AvailableFreeSpace > 512L * 1024 * 1024;
            Check(results, "Sufficient disk space", enoughSpace, "Less than 512 MB free on drive.", false);
        }
        catch { }

        // Recent crash warning
        var rphLog = Path.Combine(gtaPath, "RagePluginHook.log");
        if (File.Exists(rphLog))
        {
            var age = DateTime.Now - new FileInfo(rphLog).LastWriteTime;
            if (age.TotalHours < 2)
                results.Add(new PreLaunchCheckResult { CheckName = "Recent crash detected", Passed = false, Message = "RagePluginHook.log was updated less than 2 hours ago — possible recent crash.", IsBlocker = false });
        }

        return results;
    }

    private static void Check(List<PreLaunchCheckResult> results, string name, bool passed, string? message, bool isBlocker) =>
        results.Add(new PreLaunchCheckResult { CheckName = name, Passed = passed, Message = passed ? null : message, IsBlocker = isBlocker });
}
