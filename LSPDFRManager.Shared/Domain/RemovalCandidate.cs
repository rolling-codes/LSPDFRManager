namespace LSPDFRManager.Domain;

public sealed class RemovalCandidate
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public required CandidateClassification Classification { get; init; }
    public required CleanupRiskLevel RiskLevel { get; init; }
    public required string Reason { get; init; }
    public bool IsDirectory { get; init; }
    public long? SizeBytes { get; init; }
}
