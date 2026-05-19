namespace LSPDFRManager.Domain;

public enum FindingConfidence { Low, Medium, High }

public sealed record RageLogEntry(
    string SourceLog,
    int LineNumber,
    DateTimeOffset? Timestamp,
    CrashLogSeverity Severity,
    string? Component,
    string Message,
    string RawLine,
    IReadOnlyList<string> ContinuationLines
);

public sealed record RageLogFinding(
    string Code,
    CrashLogSeverity Severity,
    FindingConfidence Confidence,
    string Title,
    string Explanation,
    string? AffectedPlugin,
    string? AffectedFile,
    string? MissingDependency,
    IReadOnlyList<string> EvidenceLines,
    IReadOnlyList<string> SuggestedFixes
);

public sealed record RageLogSession(
    string SourceLog,
    DateTimeOffset? StartedAt,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<RageLogEntry> Entries,
    IReadOnlyList<RageLogFinding> Findings
);
