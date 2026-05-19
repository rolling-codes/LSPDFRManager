namespace LSPDFRManager.Domain;

public sealed class CleanupApplyResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<string> DeletedPaths { get; init; }
    public required IReadOnlyList<string> FailedPaths { get; init; }
    public string? BackupZipPath { get; init; }
    public string? AbortReason { get; init; }
}
