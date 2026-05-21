namespace LSPDFRManager.Domain;

public class SettingsValidationResult
{
    public string SettingName { get; init; } = "";
    public string Issue { get; init; } = "";
    public string? SuggestedFix { get; init; }
    public bool IsBlocking { get; init; }
}
