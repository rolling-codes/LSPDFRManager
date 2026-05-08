namespace LSPDFRManager.Domain;

public class InstallPlan
{
    public string ArchiveSource { get; init; } = "";
    public ModType DetectedType { get; init; }
    public float Confidence { get; init; }
    public List<InstallPlanEntry> Entries { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public string? ReadmeContent { get; init; }
    public bool IsDryRun { get; init; }
}
