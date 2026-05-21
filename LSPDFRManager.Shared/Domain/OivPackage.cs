namespace LSPDFRManager.Domain;

public class OivPackage
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string TargetGame { get; set; } = "Grand Theft Auto V";
    public List<OivFileEntry> Files { get; set; } = [];
    public string? SourcePath { get; set; }   // Path to .oiv file (when installing)
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
}

public class OivFileEntry
{
    public string SourcePath { get; set; } = "";    // Relative path inside OIV content/
    public string InstallPath { get; set; } = "";   // Absolute or relative target path
    public OivFileAction Action { get; set; }       // Add, Replace, Skip
    public bool IsUserEdited { get; set; }           // True when user manually edited InstallPath
}

public enum OivFileAction { Add, Replace, Skip }
