namespace LSPDFRManager.Domain;

public sealed class CrashTimelineFinding
{
    public string? SuspectedPlugin { get; init; }
    public string? SuspectedComponent { get; init; }
    public float Confidence { get; init; }
    public CrashLogSeverity Severity { get; init; }
    public string[] EvidenceLines { get; init; } = [];
    public string RecommendedAction { get; init; } = "";
    public CrashTimelineEvent[] Timeline { get; init; } = [];
}
