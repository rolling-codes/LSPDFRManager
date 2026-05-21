namespace LSPDFRManager.OivPipeline.Models;

public sealed class BundleManifest
{
    public required string Type { get; init; }
    public string? PackageName { get; init; }
    public string? DlcPackName { get; init; }
    public string? ReplaceSlot { get; init; }
    public string? SourceFolder { get; init; }
    public bool ConfigOnly { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public string? TargetArchivePath { get; init; }
}

public sealed class ManifestReadResult
{
    public BundleManifest? Manifest { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public bool IsValid => Manifest is not null && ValidationErrors.Count == 0;
}
