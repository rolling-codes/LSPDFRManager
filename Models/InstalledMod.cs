namespace LSPDFRManager.Models;

public class InstalledMod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ModType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public string TypeColor { get; set; } = "#6B7280";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string DlcPackName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public List<string> InstalledFiles { get; set; } = [];
    public DateTime InstalledAt { get; set; } = DateTime.Now;
}
