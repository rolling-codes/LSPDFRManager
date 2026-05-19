namespace LSPDFRManager.Domain;

public class PluginScanResult
{
    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public string Issue { get; init; } = "";
    public string? RecommendedFix { get; init; }
    public PluginScanSeverity Severity { get; init; }
}
