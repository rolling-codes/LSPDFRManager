using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class CleanupApplyService
{
    private readonly GtaFileBackupService _backup;

    public CleanupApplyService() : this(new GtaFileBackupService()) { }

    internal CleanupApplyService(GtaFileBackupService backup) => _backup = backup;

    public async Task<CleanupApplyResult> ApplyAsync(
        string gtaRoot,
        IReadOnlyList<RemovalCandidate> selectedCandidates,
        CleanupMode mode,
        CancellationToken cancellationToken = default)
    {
        if (selectedCandidates.Count == 0)
        {
            return Abort("No candidates selected.");
        }

        // Safety: reject blocked or outside-root candidates before any I/O
        var rootPrefix = Path.GetFullPath(gtaRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var c in selectedCandidates)
        {
            if (c.Classification == CandidateClassification.Blocked)
                return Abort($"Cannot delete blocked file: {c.RelativePath}");

            if (!Path.GetFullPath(c.FullPath)
                    .StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return Abort($"Candidate is outside GTA root: {c.RelativePath}");
        }

        // Backup first — abort with zero deletions on failure
        var backup = await _backup.CreateCleanupBackupAsync(
            gtaRoot, selectedCandidates, mode, cancellationToken);

        if (!backup.Success)
        {
            return new CleanupApplyResult
            {
                Success = false,
                DeletedPaths = [],
                FailedPaths = backup.FailedPaths.ToList(),
                AbortReason = backup.ErrorMessage ?? "Backup failed. No files were deleted.",
            };
        }

        // Delete
        var deleted = new List<string>();
        var failed = new List<string>();

        foreach (var candidate in selectedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (candidate.IsDirectory)
                {
                    if (Directory.Exists(candidate.FullPath))
                    {
                        Directory.Delete(candidate.FullPath, recursive: true);
                        deleted.Add(candidate.RelativePath);
                    }
                }
                else
                {
                    if (File.Exists(candidate.FullPath))
                    {
                        File.Delete(candidate.FullPath);
                        deleted.Add(candidate.RelativePath);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[CleanupApply] Failed to delete '{candidate.RelativePath}': {ex.Message}");
                failed.Add(candidate.RelativePath);
            }
        }

        // Verify deletions
        var verificationFailed = new List<string>();
        foreach (var candidate in selectedCandidates)
        {
            if (!deleted.Contains(candidate.RelativePath)) continue;

            var stillExists = candidate.IsDirectory
                ? Directory.Exists(candidate.FullPath)
                : File.Exists(candidate.FullPath);

            if (stillExists)
                verificationFailed.Add(candidate.RelativePath);
        }

        if (verificationFailed.Count > 0)
        {
            return new CleanupApplyResult
            {
                Success = false,
                DeletedPaths = deleted,
                FailedPaths = verificationFailed,
                BackupZipPath = backup.ZipPath,
                AbortReason =
                    $"Verification failed: {verificationFailed.Count} item(s) still exist after deletion.",
            };
        }

        return new CleanupApplyResult
        {
            Success = failed.Count == 0,
            DeletedPaths = deleted,
            FailedPaths = failed,
            BackupZipPath = backup.ZipPath,
        };
    }

    private static CleanupApplyResult Abort(string reason) =>
        new() { Success = false, DeletedPaths = [], FailedPaths = [], AbortReason = reason };
}
