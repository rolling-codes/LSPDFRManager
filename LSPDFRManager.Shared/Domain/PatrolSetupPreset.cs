namespace LSPDFRManager.Domain;

public sealed class PatrolSetupPreset
{
    public string PresetId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public PresetPatchRule[] Rules { get; init; } = [];
}
