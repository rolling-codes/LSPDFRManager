namespace LSPDFRManager.Domain;

public class EmergeryRecoveryAction
{
    public string Description { get; init; } = "";
    public string AffectedPath { get; init; } = "";
    public bool WillDisable { get; init; }
}

public class EmergencyRecoveryPlan
{
    public string Mode { get; init; } = "";
    public List<EmergeryRecoveryAction> Actions { get; init; } = [];
    public string? RestorePointId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
