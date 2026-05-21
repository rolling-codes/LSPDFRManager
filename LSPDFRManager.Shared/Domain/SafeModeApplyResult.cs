namespace LSPDFRManager.Domain;

public sealed record SafeModeApplyResult(
    bool Success,
    int FilesDisabled,
    int FilesFailed,
    string RestorePointId,
    IReadOnlyList<string> FailedPaths,
    string StatusMessage);
