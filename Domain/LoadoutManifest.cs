namespace LSPDFRManager.Domain;

public class LoadoutManifest
{
    public string ExportedBy { get; set; } = "LSPDFR Manager";
    public string ManagerVersion { get; set; } = "3.5.0";
    public string? GameVersion { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<string> EnabledMods { get; set; } = [];
    public List<string> DisabledMods { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public string? Notes { get; set; }
}
