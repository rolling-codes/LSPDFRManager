namespace LSPDFRManager.Models;

public class ModManifest
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<ManifestEntry> Mods { get; set; } = [];
}

public class ManifestEntry
{
    public Guid OriginalId { get; set; }
    public string Name { get; set; } = "";
    public ModType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string SourceArchivePath { get; set; } = "";
    public string DlcPackName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public List<string> InstalledFiles { get; set; } = [];
    public List<ConfigSnapshot> Configs { get; set; } = [];
}

public class ConfigSnapshot
{
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    public string RelativeInstallPath { get; set; } = "";
}
