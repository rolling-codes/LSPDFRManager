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

    public OpenIvExecutor(IXmlPatcher xmlPatcher)
    {
        _xmlPatcher = xmlPatcher;
    }

    /// <summary>
    /// Executes plan: extracts files from archive, applies XML patches.
    /// Supports cancellation; rolls back all files on any failure.
    /// Returns InstallResult with success/failure/rollback state.
    /// </summary>
    public async Task<InstallResult> ExecuteAsync(
        OpenIvInstallPlan plan,
        IArchive archive,
        string targetRoot,
        CancellationToken ct = default)
    {
        var writtenFiles = new Stack<string>();

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

                var destPath = Path.Combine(targetRoot, operation.DestinationPath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                writtenFiles.Push(destPath);

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
                var xmlPatch = new XmlPatch
                {
                    FilePath = patchFilePath,
                    XPath = patch.XPath,
                    Value = patch.Value
                };
                _xmlPatcher.Apply(xmlPatch);
            }

            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count
            };
        }
        catch (Exception ex)
        {
            int writtenCount = writtenFiles.Count;
            await RollbackAsync(writtenFiles, ct);

            return new InstallResult
            {
                Success = false,
                IsPartial = writtenCount > 0,
                FilesWritten = writtenCount,
                Error = ex.Message
            };
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

    private static async Task SafeCopyAsync(
        Stream source,
        string destPath,
        long fileSize,
        CancellationToken ct)
    {
        bool canRetry = source.CanSeek;
        int bufferSize = SelectBufferSize(fileSize);
        int backoff = InitialBackoffMs;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                using (var destFile = File.Create(destPath))
                {
                    await source.CopyToAsync(destFile, bufferSize, ct);
                }

                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1 && canRetry)
            {
                await Task.Delay(backoff, ct);
                backoff *= 2;
                source.Seek(0, SeekOrigin.Begin);
            }
        }

        throw new InvalidOperationException(
            $"Failed to write file after {MaxRetries} attempts: {destPath}");
    }

    private static Task RollbackAsync(Stack<string> files, CancellationToken ct)
    {
        while (files.Count > 0)
        {
            var file = files.Pop();

            try
            {
                ct.ThrowIfCancellationRequested();

                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }
}
