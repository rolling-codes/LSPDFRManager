namespace LSPDFRManager.Domain;

public sealed class BackupUniformMapping
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string DisplayName { get; init; } = "";
    public string Agency { get; init; } = "";
    public string Region { get; init; } = "";
    public string UnitType { get; init; } = "";
    public string? PlayerOutfitName { get; init; }
    public string? BackupPedModel { get; init; }
    public string? SourceConfigRelativePath { get; init; }
    public Dictionary<string, string> Components { get; init; } = [];
    public Dictionary<string, string> Props { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = [];

    // EUP compatibility fields
    public EupGender Gender { get; init; } = EupGender.Unknown;
    public string? RequiredPedModel { get; init; }
    public string? TargetPedModel { get; init; }
    public bool RequiresFreemodePed { get; init; } = true;
}
