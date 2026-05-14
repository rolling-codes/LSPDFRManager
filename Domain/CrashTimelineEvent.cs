namespace LSPDFRManager.Domain;

public sealed class CrashTimelineEvent
{
    public DateTime? Timestamp { get; init; }
    public string Source { get; init; } = "";
    public string Message { get; init; } = "";
    public string? RelatedPlugin { get; init; }
    public CrashLogSeverity Severity { get; init; }
}
