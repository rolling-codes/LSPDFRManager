namespace LSPDFRManager.Domain;

/// <summary>
/// Result of an install operation, used to communicate success/failure/partial state to callers.
/// </summary>
public class InstallResult
{
    public bool Success { get; init; }
    public bool IsPartial { get; init; }
    public string? Error { get; init; }
    public int FilesWritten { get; init; }
    public string? FailedEntry { get; init; }
}
