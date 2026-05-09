namespace LSPDFRManager.Domain;

public class RestorePointEntry
{
    public string RelativePath { get; init; } = "";
    public bool WasEnabled { get; init; }
    public string? CopiedConfigPath { get; init; }
}
