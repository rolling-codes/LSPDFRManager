namespace LSPDFRManager.Domain;

public enum InstallFailureCategory
{
    None = 0,
    Validation = 1,
    MissingPath = 2,
    MissingFile = 3,
    PermissionDenied = 4,
    InvalidArchive = 5,
    Cancelled = 6,
    Unexpected = 7,
}

/// <summary>
/// Result of an install operation, used to communicate success/failure/partial state to callers.
/// </summary>
public class InstallResult
{
    public bool Success { get; init; }
    public bool IsPartial { get; init; }
    public string? Error { get; init; }
    public string? UserMessage { get; init; }
    public InstallFailureCategory FailureCategory { get; init; } = InstallFailureCategory.None;
    public int FilesWritten { get; init; }
    public string? FailedEntry { get; init; }
    public List<string> WrittenFiles { get; init; } = [];

    /// <summary>Files newly created by this install (no prior version existed).</summary>
    public List<TransactionFileRecord> AddedFileRecords { get; init; } = [];

    /// <summary>Files that had a prior version; backups are in the transaction backup folder.</summary>
    public List<TransactionFileRecord> OverwrittenFileRecords { get; init; } = [];

    /// <summary>
    /// Errors that occurred during rollback after a failed install.
    /// Non-empty means the filesystem may not be fully restored to pre-install state.
    /// The original install failure is always in <see cref="Error"/>.
    /// </summary>
    public List<string> RollbackErrors { get; init; } = [];

    public bool RollbackSucceeded => !Success && RollbackErrors.Count == 0;
}
