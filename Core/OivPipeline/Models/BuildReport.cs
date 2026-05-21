namespace LSPDFRManager.OivPipeline.Models;

public sealed class BuildReport
{
    public string PackageName { get; init; } = "";
    public BundleManifest? ManifestData { get; init; }
    public string? DetectedType { get; init; }
    public string? ConfidenceSource { get; init; }
    public IReadOnlyList<ValidationGate> ValidationResults { get; init; } = [];
    public IReadOnlyList<InstallOperation> InstallOperations { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> RefusalReasons { get; init; } = [];
    public IReadOnlyDictionary<string, string> FileHashes { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset Timestamp { get; init; }
    public string? AppVersion { get; init; }
    public bool DryRun { get; init; }
}

public sealed class PipelineResult
{
    public bool Success { get; init; }
    public BuildReport Report { get; init; } = new();
    public string? ReportPath { get; init; }
    public IReadOnlyList<string> RefusalReasons { get; init; } = [];
}
