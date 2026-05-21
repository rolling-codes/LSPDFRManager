namespace LSPDFRManager.Domain;

public sealed class ModTypeRecipe
{
    public string Name { get; init; } = "";
    public ModType Type { get; init; }
    public string[] ExpectedFiles { get; init; } = [];
    public string[] ExpectedFolders { get; init; } = [];
    public string[] ConfigFiles { get; init; } = [];
    public string[] CommonWrongPaths { get; init; } = [];
    public string[] Dependencies { get; init; } = [];
    public string[] SupportedPresetIds { get; init; } = [];
    public string HelpText { get; init; } = "";
}
