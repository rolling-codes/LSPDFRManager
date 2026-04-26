using LSPDFRManager.Core;
using LSPDFRManager.OpenIv.CarInstall.Models;

namespace LSPDFRManager.OpenIv.CarInstall;

public static class CarInstallAnalyzer
{
    public static CarInstallType Analyze(IEnumerable<string> archiveEntries)
    {
        var entries = archiveEntries.ToList();

        var hasYft = entries.Any(e => e.EndsWith(".yft", StringComparison.OrdinalIgnoreCase));
        var hasYtd = entries.Any(e => e.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase));
        var hasMeta = entries.Any(e => IsMetaFile(e));
        var hasDlcRpf = entries.Any(e => e.Contains("dlc.rpf", StringComparison.OrdinalIgnoreCase));
        var hasDlcPacks = entries.Any(e => e.Contains("dlcpacks", StringComparison.OrdinalIgnoreCase));

        // Rule 2: Full DLC override wins (highest priority)
        if (hasDlcRpf || hasDlcPacks)
        {
            AppLogger.Info("CarInstallAnalyzer: Detected AddonDLC (dlc.rpf or dlcpacks structure)");
            return CarInstallType.AddonDLC;
        }

        // Rule 3: ConfigPatch (meta-only)
        if ((hasYft || hasYtd || hasMeta) && !hasYft && !hasYtd && hasMeta)
        {
            AppLogger.Info("CarInstallAnalyzer: Detected ConfigPatch (meta files only)");
            return CarInstallType.ConfigPatch;
        }

        // Rule 1: Mixed archives default to ReplaceVehicle
        if (hasYft || hasYtd)
        {
            AppLogger.Info("CarInstallAnalyzer: Detected ReplaceVehicle (yft/ytd assets)");
            return CarInstallType.ReplaceVehicle;
        }

        // Ambiguity fallback
        AppLogger.Warning("CarInstallAnalyzer: Cannot classify archive confidently. No clear yft/ytd/meta/dlc.rpf found.");
        throw new InvalidOperationException("Archive structure does not match any known car mod type (ReplaceVehicle, AddonDLC, ConfigPatch).");
    }

    private static bool IsMetaFile(string path)
    {
        var filename = Path.GetFileName(path).ToLowerInvariant();
        return filename == "handling.meta" ||
               filename == "vehicles.meta" ||
               filename == "carcols.meta" ||
               filename == "carvariations.meta";
    }
}
