namespace LSPDFRManager.Domain;

public class SafeLaunchChange
{
    public string FilePath { get; init; } = "";
    public bool WasEnabled { get; init; }
    public bool WillBeEnabled { get; init; }
}
