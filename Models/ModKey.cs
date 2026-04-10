namespace LSPDFRManager.Models;

public class ModKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssociatedModId { get; set; }
    public string ModName { get; set; } = "";
    public string KeyFileName { get; set; } = "";
    public string KeyContent { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
