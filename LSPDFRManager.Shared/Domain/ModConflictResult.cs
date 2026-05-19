namespace LSPDFRManager.Domain;

public class ModConflictResult
{
    public string ConflictGroup { get; init; } = "";
    public List<string> InvolvedFiles { get; init; } = [];
    public string Reason { get; init; } = "";
    public string? SuggestedFix { get; init; }
    public string? SafeRecommendation { get; init; }
    public ConflictSeverity Severity { get; init; }
}
