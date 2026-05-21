namespace LSPDFRManager.Domain;

public sealed class CleanupScanResult
{
    public required string GtaRoot { get; init; }
    public required IReadOnlyList<RemovalGroup> Groups { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }

    public IEnumerable<RemovalCandidate> AllCandidates =>
        Groups.SelectMany(g => g.Candidates);
}
