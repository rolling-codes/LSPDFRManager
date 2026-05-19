using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.OpenIv.CarInstall.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.OpenIv.CarInstall;

/// <summary>
/// Executes a validated OpenIvInstallPlan with resilience:
/// 1. Extracts files from archive to target root (with retry on transient IO failures)
/// 2. Applies XML patches
/// 3. Stack-based LIFO rollback on any failure (deterministic, fail-fast)
/// 4. Full CancellationToken support for responsive cancellation
///
/// Stack-based rollback ensures LIFO order: last file written = first file rolled back.
/// SafeCopy retries on lock contention (up to 3 attempts, 50ms→100ms→200ms backoff).
/// Assumes plan is valid (Validator has passed).
/// </summary>
public class OpenIvExecutor
{
    private readonly IXmlPatcher _xmlPatcher;
    private const int MaxRetries = 3;
    private const int InitialBackoffMs = 50;

    private sealed record RollbackEntry(string DestinationPath, string? BackupPath)
    {
        public bool ExistedBeforeInstall => BackupPath is not null;
    }

    public OpenIvExecutor(IXmlPatcher xmlPatcher)
    {
        _xmlPatcher = xmlPatcher;
    }

    /// <summary>
    /// Executes plan: extracts files from archive, applies XML patches.
    /// Supports cancellation; rolls back all files on any failure.
    /// New files are deleted on rollback; overwritten files are restored from backup.
    /// Returns InstallResult with success/failure/rollback state.
    /// </summary>
    public async Task<InstallResult> ExecuteAsync(
        OpenIvInstallPlan plan,
        IArchive archive,
        string targetRoot,
        CancellationToken ct = default)
    {
        var rollbackEntries = new Stack<RollbackEntry>();
        var backupRoot = CreateBackupRoot(targetRoot);

        try
        {
            // 1. Extract files from archive
            foreach (var operation in plan.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var sourceEntry = archive.Entries
                    .FirstOrDefault(e => e.Key == operation.SourcePath);

                if (sourceEntry is null)
                    throw new InvalidOperationException(
                        $"Archive entry not found: {operation.SourcePath}");

                var destPath = GetSafeModsPath(targetRoot, operation.DestinationPath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                // Back up any existing file before overwriting
                string? backupPath = null;
                if (File.Exists(destPath))
                {
                    backupPath = Path.Combine(backupRoot, Guid.NewGuid().ToString("N") + ".bak");
                    File.Copy(destPath, backupPath, overwrite: false);
                }

                rollbackEntries.Push(new RollbackEntry(destPath, backupPath));

                using (var entryStream = sourceEntry.OpenEntryStream())
                {
                    await SafeCopyAsync(entryStream, destPath, sourceEntry.Size, ct);
                }
            }

            // 2. Apply XML patches
            foreach (var patch in plan.XmlPatches)
            {
                ct.ThrowIfCancellationRequested();

                var patchFilePath = Path.Combine(targetRoot, patch.FilePath);
                AppLogger.Info($"[PATCH_APPLY] {Path.GetFileName(patchFilePath)} | xpath={patch.XPath}");
                var xmlPatch = new XmlPatch
                {
                    FilePath = patchFilePath,
                    XPath = patch.XPath,
                    Value = patch.Value
                };
                _xmlPatcher.Apply(xmlPatch);
                AppLogger.Info($"[PATCH_OK] {Path.GetFileName(patchFilePath)}");
            }

            AppLogger.Info($"[PLAN_SUCCESS] operations={plan.Operations.Count} | patches={plan.XmlPatches.Count}");
            DeleteBackupRoot(backupRoot);
            return new InstallResult
            {
                Success = true,
                FilesWritten = rollbackEntries.Count
            };
        }
        catch (Exception ex)
        {
            int writtenCount = rollbackEntries.Count;
            AppLogger.Error($"[PLAN_ERROR] written={writtenCount}", ex);
            await RollbackAsync(rollbackEntries, ct);
            DeleteBackupRoot(backupRoot);

            return new InstallResult
            {
                Success = false,
                IsPartial = writtenCount > 0,
                FilesWritten = writtenCount,
                Error = ex.Message
            };
        }
    }

    private static string CreateBackupRoot(string targetRoot)
    {
        var safeRoot = Directory.Exists(targetRoot) ? targetRoot : Path.GetTempPath();
        var backupRoot = Path.Combine(safeRoot, $".lspdfrmanager_rollback_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupRoot);
        return backupRoot;
    }

    private static void DeleteBackupRoot(string backupRoot)
    {
        try
        {
            if (Directory.Exists(backupRoot))
                Directory.Delete(backupRoot, recursive: true);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"[ROLLBACK_BACKUP_CLEANUP] {ex.Message}");
        }
    }

    private static int SelectBufferSize(long fileSize)
    {
        if (fileSize < 1_000_000)
            return 65_536;        // 64KB for small
        if (fileSize < 100_000_000)
            return 524_288;       // 512KB for medium
        return 2_097_152;         // 2MB for large
    }

    private static string GetSafeModsPath(string targetRoot, string destinationPath)
    {
        var destPath = PathSafety.GetSafePath(targetRoot, destinationPath);
        var relativePath = Path.GetRelativePath(targetRoot, destPath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var normalizedPath = relativePath.ToLowerInvariant();

        if (normalizedPath == "mods" || normalizedPath.StartsWith($"mods{Path.DirectorySeparatorChar}"))
            return destPath;

        throw new InvalidOperationException($"Path traversal detected: {destinationPath}");
    }

    private static async Task SafeCopyAsync(
        Stream source,
        string destPath,
        long fileSize,
        CancellationToken ct)
    {
        bool canRetry = source.CanSeek;
        int bufferSize = SelectBufferSize(fileSize);
        int backoff = InitialBackoffMs;
        var fileName = Path.GetFileName(destPath);

        AppLogger.Info($"[COPY_START] {fileName} | size={fileSize} | seekable={canRetry} | buffer={bufferSize}");

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                using (var destFile = File.Create(destPath))
                {
                    await source.CopyToAsync(destFile, bufferSize, ct);
                }

                AppLogger.Info($"[COPY_OK] {fileName}");
                return;
            }
            catch (IOException ex) when (attempt < MaxRetries - 1 && canRetry)
            {
                AppLogger.Warning($"[COPY_RETRY] {fileName} | attempt={attempt + 1}/{MaxRetries} | backoff={backoff}ms | reason={ex.Message}");
                await Task.Delay(backoff, ct);
                backoff *= 2;
                source.Seek(0, SeekOrigin.Begin);
            }
        }

        AppLogger.Error($"[COPY_FAILED] {fileName} | exhausted {MaxRetries} attempts");
        throw new InvalidOperationException(
            $"Failed to write file after {MaxRetries} attempts: {destPath}");
    }

    private static Task RollbackAsync(Stack<RollbackEntry> entries, CancellationToken ct)
    {
        int rollbackCount = entries.Count;
        AppLogger.Info($"[ROLLBACK_START] {rollbackCount} files");

        int restoredCount = 0;
        int deletedCount = 0;

        while (entries.Count > 0)
        {
            var entry = entries.Pop();

            try
            {
                ct.ThrowIfCancellationRequested();

                if (entry.ExistedBeforeInstall && entry.BackupPath is not null && File.Exists(entry.BackupPath))
                {
                    // Restore the original file that was overwritten
                    if (File.Exists(entry.DestinationPath))
                        File.Delete(entry.DestinationPath);
                    File.Move(entry.BackupPath, entry.DestinationPath);
                    restoredCount++;
                    AppLogger.Info($"[ROLLBACK_RESTORE] {Path.GetFileName(entry.DestinationPath)}");
                }
                else if (File.Exists(entry.DestinationPath))
                {
                    // Newly created file — remove it
                    File.Delete(entry.DestinationPath);
                    deletedCount++;
                    AppLogger.Info($"[ROLLBACK_DELETE] {Path.GetFileName(entry.DestinationPath)}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[ROLLBACK_ERROR] {Path.GetFileName(entry.DestinationPath)} | {ex.Message}");
            }
        }

        AppLogger.Info($"[ROLLBACK_COMPLETE] restored={restoredCount} deleted={deletedCount} of {rollbackCount}");
        return Task.CompletedTask;
    }
}
