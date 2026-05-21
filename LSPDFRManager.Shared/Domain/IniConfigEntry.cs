namespace LSPDFRManager.Domain;

public enum IniValueType { String, Bool, Int, Float, Keybind, Path, Color, Enum }

/// <summary>
/// A single parsed setting from a .ini file, with type inference and write-back metadata.
/// </summary>
public class IniConfigEntry
{
    public string FilePath  { get; set; } = "";
    public string Section   { get; set; } = "";
    public string Key       { get; set; } = "";
    public string RawValue  { get; set; } = "";
    public string EditValue { get; set; } = "";
    public IniValueType InferredType { get; set; }
    public string? Comment  { get; set; }
    public bool    IsDirty  { get; set; }

    public string SectionDisplay => string.IsNullOrEmpty(Section) ? "(General)" : Section;
    public string DisplayLabel   => Key;
}
