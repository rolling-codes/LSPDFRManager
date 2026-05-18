namespace LSPDFRManager.Domain;

public sealed class RemovalGroup
{
    public required string Label { get; init; }
    public required CandidateClassification GroupKind { get; init; }
    public required IReadOnlyList<RemovalCandidate> Candidates { get; init; }
}
