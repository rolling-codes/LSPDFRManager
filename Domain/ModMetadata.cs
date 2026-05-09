namespace LSPDFRManager.Domain;

public class ModMetadata
{
    public string ModId { get; set; } = "";
    public string? CustomName { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? SourceUrl { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public bool IsRisky { get; set; }
    public bool IgnoredByDiagnostics { get; set; }
}
