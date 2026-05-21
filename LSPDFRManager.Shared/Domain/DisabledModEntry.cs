namespace LSPDFRManager.Domain;

public class DisabledModEntry
{
    public string DisabledPath { get; init; } = "";
    public string OriginalName { get; init; } = "";
    public string Category { get; init; } = "";
    public string ContainingFolder { get; init; } = "";
    public string? LikelyModName { get; init; }
}
