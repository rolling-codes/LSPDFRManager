namespace LSPDFRManager.Domain;

public sealed class EupUniformDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string DisplayName { get; init; } = "";
    public string Department { get; init; } = "Unknown";
    public string County { get; init; } = "Unknown";
    public string Region { get; init; } = "Unknown";
    public EupGender Gender { get; init; } = EupGender.Unknown;
    public string SourceRelativePath { get; init; } = "";
    public string? PedModel { get; init; }
    public Dictionary<string, string> Components { get; init; } = [];
    public Dictionary<string, string> Props { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = [];
    public float Confidence { get; init; } = 0.5f;
}
