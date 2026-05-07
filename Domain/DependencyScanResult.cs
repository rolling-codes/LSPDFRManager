namespace LSPDFRManager.Domain;

public class DependencyScanResult
{
    public string Name { get; init; } = "";
    public string ExpectedPath { get; init; } = "";
    public string? ActualPath { get; init; }
    public DependencyStatus Status { get; init; }
    public string? Version { get; init; }
    public string? Note { get; init; }
    public bool IsIgnored { get; set; }
}
