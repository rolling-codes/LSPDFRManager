using LSPDFRManager.Domain;

namespace LSPDFRManager.Services.Modes;

public sealed class SafeCoreResetMode : ICleanupMode
{
    public CleanupMode Mode => CleanupMode.SafeCoreReset;

    public CleanupModePreset Apply(CleanupScanResult scanResult)
    {
        var selected = new HashSet<Guid>();

        var coreGroup = scanResult.Groups
            .FirstOrDefault(g => g.GroupKind == CandidateClassification.LspdfrCore);

        if (coreGroup is not null)
        {
            foreach (var c in coreGroup.Candidates)
                selected.Add(c.Id);
        }

        // Show all groups except ManualReview; default-select LspdfrCore only
        var visibleGroups = scanResult.Groups
            .Where(g => g.GroupKind != CandidateClassification.ManualReview)
            .ToList();

        return new CleanupModePreset
        {
            Mode = CleanupMode.SafeCoreReset,
            Groups = visibleGroups,
            DefaultSelectedIds = selected,
            RiskLevel = CleanupRiskLevel.Low,
            WarningText =
                "This will remove the LSPDFR core DLL. Third-party plugins, data folders, " +
                "RPH, and shared dependencies are not selected by default.",
            ConfirmPhrase = null,
            RequireBackup = true,
        };
    }
}
