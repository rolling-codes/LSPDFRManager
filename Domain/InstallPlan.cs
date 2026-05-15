namespace LSPDFRManager.Domain;

public class InstallPlan
{
    public string ArchiveSource { get; init; } = "";
    public ModType DetectedType { get; init; }
    public float Confidence { get; init; }
    public List<InstallPlanEntry> Entries { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> BlockingIssues { get; init; } = [];
    public string? ReadmeContent { get; init; }
    public bool IsDryRun { get; init; }
    public List<string> InstallOrder { get; init; } = [];
    public List<string> OrderReasons { get; init; } = [];
    public bool RequiresManualConfirmation { get; init; }
}
