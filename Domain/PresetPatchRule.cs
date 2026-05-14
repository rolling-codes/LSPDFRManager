namespace LSPDFRManager.Domain;

public sealed class PresetPatchRule
{
    public string File { get; init; } = "";
    public string[] MatchKeys { get; init; } = [];
    public string SetValue { get; init; } = "";
    public string Reason { get; init; } = "";
}
