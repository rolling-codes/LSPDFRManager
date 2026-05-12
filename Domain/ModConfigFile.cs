namespace LSPDFRManager.Domain;

public class ModConfigFile
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    public string? BackupPath { get; set; }
    public bool IsModified { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
    public DateTime LastModified { get; set; }
}
