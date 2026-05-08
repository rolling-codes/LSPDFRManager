namespace LSPDFRManager.Domain;

public class RestorePoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OperationName { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RestorePointEntry> Entries { get; set; } = [];
}
