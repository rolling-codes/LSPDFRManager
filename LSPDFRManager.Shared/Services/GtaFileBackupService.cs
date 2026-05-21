using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class GtaFileBackupService
{
    public virtual async Task<CleanupBackupResult> CreateCleanupBackupAsync(
        string gtaRoot,
        IReadOnlyList<RemovalCandidate> selectedCandidates,
        CleanupMode mode,
        CancellationToken cancellationToken = default)
    {
        var backupFolder = ResolveBackupFolder();
        if (backupFolder is null)
        {
            return new CleanupBackupResult
            {
                Success = false,
                FailedPaths = [],
                ErrorMessage =
                    "No valid backup folder configured. " +
                    "Configure a backup folder in Settings before running cleanup.",
            };
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var zipPath = Path.Combine(backupFolder, $"lspdfr_cleanup_backup_{timestamp}.zip");
        var failedPaths = new List<string>();

        try
        {
            Directory.CreateDirectory(backupFolder);

            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

                WriteManifest(zip, gtaRoot, selectedCandidates, mode, timestamp);

                foreach (var candidate in selectedCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.IsDirectory)
                    {
                        if (!Directory.Exists(candidate.FullPath)) continue;

                        foreach (var file in Directory.GetFiles(candidate.FullPath, "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(gtaRoot, file).Replace('\\', '/');
                            try { zip.CreateEntryFromFile(file, rel); }
                            catch { failedPaths.Add(file); }
                        }
                    }
                    else
                    {
                        if (!File.Exists(candidate.FullPath)) continue;

                        try
                        {
                            var rel = candidate.RelativePath.Replace('\\', '/');
                            zip.CreateEntryFromFile(candidate.FullPath, rel);
                        }
                        catch { failedPaths.Add(candidate.FullPath); }
                    }
                }
            }, cancellationToken);

            if (failedPaths.Count > 0)
            {
                TryDeleteFile(zipPath);
                return new CleanupBackupResult
                {
                    Success = false,
                    FailedPaths = failedPaths,
                    ErrorMessage =
                        $"Backup failed: {failedPaths.Count} file(s) could not be backed up. " +
                        "No files will be deleted.",
                };
            }

            return new CleanupBackupResult
            {
                Success = true,
                ZipPath = zipPath,
                FailedPaths = [],
            };
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(zipPath);
            return new CleanupBackupResult
            {
                Success = false,
                FailedPaths = failedPaths,
                ErrorMessage = "Backup was cancelled.",
            };
        }
        catch (Exception ex)
        {
            TryDeleteFile(zipPath);
            return new CleanupBackupResult
            {
                Success = false,
                FailedPaths = failedPaths,
                ErrorMessage = $"Backup failed: {ex.Message}",
            };
        }
    }

    private static void WriteManifest(
        ZipArchive zip,
        string gtaRoot,
        IReadOnlyList<RemovalCandidate> candidates,
        CleanupMode mode,
        string timestamp)
    {
        var version = typeof(GtaFileBackupService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        var manifest = new
        {
            BackupTimestamp = timestamp,
            GtaRoot = gtaRoot,
            CleanupMode = mode.ToString(),
            AppVersion = version,
            Files = candidates.Select(c => new
            {
                c.RelativePath,
                c.IsDirectory,
                Classification = c.Classification.ToString(),
                RiskLevel = c.RiskLevel.ToString(),
                c.Reason,
                c.SizeBytes,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var entry = zip.CreateEntry("cleanup_manifest.json");
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string? ResolveBackupFolder()
    {
        var configured = AppConfig.Instance.BackupPath;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(AppDataPaths.Root, "backups");
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
