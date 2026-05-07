namespace LSPDFRManager.Domain;

public class CrashLogFinding
{
    public string SourceLog { get; init; } = "";
    public string SuspectedCause { get; init; } = "";
    public string? AffectedPlugin { get; init; }
    public string? RecommendedFix { get; init; }
    public string? RelatedFiles { get; init; }
    public string MatchedLine { get; init; } = "";
    public DateTime? Timestamp { get; init; }
    public CrashLogSeverity Severity { get; init; }
}
