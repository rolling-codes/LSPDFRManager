namespace LSPDFRManager.Models;

public class ModInfo
{
    public string Name { get; set; } = "";
    public ModType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public string TypeColor { get; set; } = "#6B7280";
    public string SourcePath { get; set; } = "";
    public List<string> Files { get; set; } = [];
    public float Confidence { get; set; }
    public string ConfidenceLabel => Confidence >= 0.75f ? "High" : Confidence >= 0.45f ? "Medium" : "Low";
    public string? Version { get; set; }
    public string? Author { get; set; }
    public bool IsAddon { get; set; }
    public string? DlcPackName { get; set; }
    public List<string> Warnings { get; set; } = [];
}
