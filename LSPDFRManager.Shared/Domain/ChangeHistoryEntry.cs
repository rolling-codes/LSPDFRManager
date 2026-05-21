namespace LSPDFRManager.Domain;

public class ChangeHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ChangeHistoryAction Action { get; set; }
    public string Description { get; set; } = "";
    public string? AffectedFile { get; set; }
    public string? Detail { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
