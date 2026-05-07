namespace LSPDFRManager.Domain;

public class PreLaunchCheckResult
{
    public string CheckName { get; init; } = "";
    public bool Passed { get; init; }
    public string? Message { get; init; }
    public bool IsBlocker { get; init; }
}
