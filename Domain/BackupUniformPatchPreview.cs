namespace LSPDFRManager.Domain;

public sealed class BackupUniformPatchPreview
{
    public string SourceFile { get; init; } = "";
    public string MappingName { get; init; } = "";
    public string[] BeforeLines { get; init; } = [];
    public string[] AfterLines { get; init; } = [];
    public string[] Warnings { get; init; } = [];
    public bool CanApply { get; init; }

    // Extended for EUP assignment preview
    public float Confidence { get; init; } = 1.0f;
    public string[] MismatchWarnings { get; init; } = [];
    public string? BackupCreatedPath { get; init; }
    public bool IsReadOnlyPreview { get; init; }

    // Stable patch target (agency + unitType + pedModel) used by XDocument-based apply
    public string? TargetAgency { get; init; }
    public string? TargetUnitType { get; init; }
    public string? TargetPedModel { get; init; }
    public string? UniformNameToApply { get; init; }
}
