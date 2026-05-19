namespace LSPDFRManager.Domain;

public sealed class CleanupModePreset
{
    public required CleanupMode Mode { get; init; }
    public required IReadOnlyList<RemovalGroup> Groups { get; init; }
    public required HashSet<Guid> DefaultSelectedIds { get; init; }
    public required CleanupRiskLevel RiskLevel { get; init; }
    public required string WarningText { get; init; }
    public string? ConfirmPhrase { get; init; }
    public bool RequireBackup { get; init; } = true;
}
