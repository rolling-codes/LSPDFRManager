namespace LSPDFRManager.Domain;

public sealed class KeybindConflict
{
    public string KeyValue { get; init; } = "";
    public ConflictEntry[] Conflicts { get; init; } = [];
    public DiagnosticSeverity Severity { get; init; }
}

public sealed class ConflictEntry
{
    public string FilePath { get; init; } = "";
    public string Section { get; init; } = "";
    public string Key { get; init; } = "";
    public string? PluginOwner { get; init; }
}
