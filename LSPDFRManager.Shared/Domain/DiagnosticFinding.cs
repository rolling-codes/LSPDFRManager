namespace LSPDFRManager.Domain;

public class DiagnosticFinding
{
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string? RecommendedFix { get; init; }
    public string? AffectedPath { get; init; }
    public DiagnosticSeverity Severity { get; init; }
    public string? AutoFixId { get; init; }
    public float Confidence { get; init; } = 1.0f;
    public DateTime ScannedAt { get; init; } = DateTime.UtcNow;
}
