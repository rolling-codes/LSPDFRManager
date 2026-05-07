namespace LSPDFRManager.Domain;

public class GamePathCandidate
{
    public string Path { get; init; } = "";
    public string Source { get; init; } = "";
    public bool IsValid { get; init; }
    public string? ValidationError { get; init; }
}
