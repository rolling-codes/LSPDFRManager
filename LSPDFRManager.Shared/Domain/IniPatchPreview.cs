namespace LSPDFRManager.Domain;

public sealed class IniPatchPreview
{
    public string FilePath { get; init; } = "";
    public string Section { get; init; } = "";
    public string Key { get; init; } = "";
    public string OldValue { get; init; } = "";
    public string NewValue { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool WouldChange { get; init; }
}
