namespace LSPDFRManager.Domain;

public class InstallPlanEntry
{
    public string ArchivePath { get; init; } = "";
    public string TargetPath { get; init; } = "";
    public bool WillOverwrite { get; init; }
    public bool IsExcluded { get; set; }
    public InstallRisk Risk { get; init; }
}
