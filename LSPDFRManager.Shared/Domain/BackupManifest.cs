namespace LSPDFRManager.Domain;

public class BackupManifest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = "";
    public string BackupType { get; set; } = "Full";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long SizeBytes { get; set; }
    public int ModCount { get; set; }
}
