namespace LSPDFRManager.Domain;

public class GtaBaseline
{
    public string? GtaVersion { get; init; }
    public string? GtaHash { get; init; }
    public long? GtaExeFileSizeBytes { get; init; }
    public DateTime? GtaExeLastWriteTimeUtc { get; init; }
    public DateTime CheckedAtUtc { get; init; }
}
