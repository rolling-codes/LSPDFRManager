using LSPDFRManager.Models;

namespace LSPDFRManager.Services;

/// <summary>
/// Checks common GTA V / LSPDFR setup issues that frequently cause mod installs
/// to fail or appear broken at runtime.
/// </summary>
public static class InstallPreflightService
{
    public static IReadOnlyList<string> Evaluate(string gtaPath, ModInfo? detectedMod)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
        {
            issues.Add("GTA V path is missing or invalid. Set it in Settings before installing.");
            return issues;
        }

        if (gtaPath.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
            issues.Add("GTA V is inside Program Files. Run manager as admin or move GTA to a writable folder to avoid permission errors.");

        var rphPath = Path.Combine(gtaPath, "RAGEPluginHook.exe");
        if (!File.Exists(rphPath))
            issues.Add("RAGEPluginHook.exe was not found in your GTA V root. LSPDFR plugins may not load correctly.");

        var lspdfrDllA = Path.Combine(gtaPath, "Plugins", "LSPDFR", "LSPD First Response.dll");
        var lspdfrDllB = Path.Combine(gtaPath, "Plugins", "LSPD First Response.dll");
        if (!File.Exists(lspdfrDllA) && !File.Exists(lspdfrDllB))
            issues.Add("LSPDFR core files were not found in Plugins. Install/update LSPDFR before adding plugins.");

        if (detectedMod is not null &&
            (detectedMod.Type == ModType.AsiMod || detectedMod.Type == ModType.Script))
        {
            var scriptHookV = Path.Combine(gtaPath, "ScriptHookV.dll");
            if (!File.Exists(scriptHookV))
                issues.Add("ScriptHookV.dll not found. Script/ASI mods commonly require ScriptHookV to work.");
        }

        var battleye = Path.Combine(gtaPath, "BattlEye");
        if (Directory.Exists(battleye))
            issues.Add("If mods fail after a title update, disable BattlEye in the Rockstar launcher before starting LSPDFR.");

        return issues;
    }
}
