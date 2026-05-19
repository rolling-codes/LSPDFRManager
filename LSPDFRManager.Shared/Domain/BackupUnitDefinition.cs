namespace LSPDFRManager.Domain;

public sealed class BackupUnitDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string DisplayName { get; init; } = "";
    public string Agency { get; init; } = "";
    public string Region { get; init; } = "";
    public string UnitType { get; init; } = "";
    public string? VehicleModel { get; init; }
    public string? PedModel { get; init; }
    public string? UniformName { get; init; }
    public string? SourceConfigRelativePath { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
