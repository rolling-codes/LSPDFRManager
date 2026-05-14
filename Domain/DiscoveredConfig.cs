namespace LSPDFRManager.Domain;

public sealed class DiscoveredConfig
{
    public string RelativePath { get; init; } = "";
    public string AbsolutePath { get; init; } = "";
    public string FileType { get; init; } = "";
    public string? PluginOwner { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime LastModified { get; init; }
}
