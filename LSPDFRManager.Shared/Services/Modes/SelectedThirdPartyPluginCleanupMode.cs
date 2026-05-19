using LSPDFRManager.Domain;

namespace LSPDFRManager.Services.Modes;

public sealed class SelectedThirdPartyPluginCleanupMode : ICleanupMode
{
    public CleanupMode Mode => CleanupMode.ThirdPartyPluginCleanup;

    public CleanupModePreset Apply(CleanupScanResult scanResult)
    {
        // Show only third-party plugin groups — no LSPDFR core, RPH, shared deps
        var pluginGroups = scanResult.Groups
            .Where(g => g.GroupKind is
                CandidateClassification.ThirdPartyPlugin or
                CandidateClassification.PluginConfig or
                CandidateClassification.PluginDataFolder)
            .ToList();

        return new CleanupModePreset
        {
            Mode = CleanupMode.ThirdPartyPluginCleanup,
            Groups = pluginGroups,
            DefaultSelectedIds = [],
            RiskLevel = CleanupRiskLevel.Medium,
            WarningText =
                "Select the plugin groups you want to remove. LSPDFR core, RPH, and shared " +
                "dependencies are not shown. Nothing is selected by default — you must choose.",
            RequireBackup = true,
        };
    }
}
