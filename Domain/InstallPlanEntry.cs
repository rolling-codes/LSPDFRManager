namespace LSPDFRManager.Domain;

public class InstallPlanEntry
{
    public string ArchivePath { get; init; } = "";
    public string TargetPath { get; init; } = "";
    public bool WillOverwrite => DestinationExists;
    public bool DestinationExists { get; init; }
    public bool IsExcluded { get; set; }
    public InstallRisk Risk { get; init; }
    public InstallOverwriteRisk OverwriteRisk { get; init; }
    public InstallConflictAction PlannedAction { get; set; }
    public string? RenamedTargetPath { get; set; }
    public string? DetectedPlugin { get; init; }
    public string? DependencyReason { get; init; }
    public string? OverwriteReason { get; init; }
    public bool RequiresExplicitConfirmation { get; init; }
}
