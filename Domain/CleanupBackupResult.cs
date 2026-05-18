namespace LSPDFRManager.Domain;

public sealed class CleanupBackupResult
{
    public required bool Success { get; init; }
    public string? ZipPath { get; init; }
    public required IReadOnlyList<string> FailedPaths { get; init; }
    public string? ErrorMessage { get; init; }
}
