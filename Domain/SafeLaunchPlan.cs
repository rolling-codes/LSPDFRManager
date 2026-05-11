namespace LSPDFRManager.Domain;

public class SafeLaunchPlan
{
    public string Mode { get; init; } = "";
    public List<SafeLaunchChange> Changes { get; init; } = [];
    public string? RestorePointId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
