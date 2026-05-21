namespace LSPDFRManager.OivPipeline.Models;

public sealed class ClassificationResult
{
    public BundleType? Type { get; init; }
    public ConfidenceSource? Source { get; init; }
    public IReadOnlyList<string> RefusalReasons { get; init; } = [];
    public bool IsClassified => Type.HasValue && RefusalReasons.Count == 0;
}
