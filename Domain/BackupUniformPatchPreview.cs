namespace LSPDFRManager.Domain;

public sealed class BackupUniformPatchPreview
{
    public string SourceFile { get; init; } = "";
    public string MappingName { get; init; } = "";
    public string[] BeforeLines { get; init; } = [];
    public string[] AfterLines { get; init; } = [];
    public string[] Warnings { get; init; } = [];
    public bool CanApply { get; init; }
}
