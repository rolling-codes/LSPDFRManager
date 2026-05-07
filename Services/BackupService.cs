using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class BackupService
{
    public async Task<string> CreateBackupAsync(IProgress<string>? progress = null)
    {
        var config = AppConfig.Instance;
        Directory.CreateDirectory(config.BackupPath);

        var backupPath = Path.Combine(
            config.BackupPath,
            $"lspmanager_backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");

        progress?.Report("Creating backup...");

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(backupPath, ZipArchiveMode.Create);

            foreach (var filePath in GetFilesToBackup())
            {
                if (!File.Exists(filePath))
                    continue;

                var relativePath = Path.GetRelativePath(AppDataPaths.Root, filePath);
                zip.CreateEntryFromFile(filePath, relativePath);
                progress?.Report($"Backed up: {relativePath}");
            }
        });

        config.LastBackupDate = DateTime.Now;
        config.Save();

        AppLogger.Info($"Backup created: {backupPath}");
        progress?.Report($"Done — {backupPath}");
        return backupPath;
    }

    public async Task RestoreFromBackupAsync(string backupPath, IProgress<string>? progress = null)
    {
        progress?.Report($"Restoring from {Path.GetFileName(backupPath)}...");
        Directory.CreateDirectory(AppDataPaths.Root);

        await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(backupPath);
            foreach (var entry in zip.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.FullName)))
            {
                var destination = Path.Combine(AppDataPaths.Root, entry.FullName);
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                entry.ExtractToFile(destination, overwrite: true);
                progress?.Report($"Restored: {entry.FullName}");
            }
        });

        AppLogger.Info($"Restored from backup: {backupPath}");
        progress?.Report("Restore complete. Please restart the application.");
    }

    public IEnumerable<string> ListBackups()
    {
        if (!Directory.Exists(AppConfig.Instance.BackupPath))
            return [];

        return Directory.GetFiles(AppConfig.Instance.BackupPath, "lspmanager_backup_*.zip")
            .OrderByDescending(file => file);
    }

    private static IEnumerable<string> GetFilesToBackup()
    {
        yield return AppDataPaths.LibraryFile;
        yield return AppDataPaths.ConfigSnapshotsFile;
        yield return AppDataPaths.ConfigFile;

        if (!Directory.Exists(AppDataPaths.KeysDirectory))
            yield break;

        foreach (var keyFile in Directory.GetFiles(AppDataPaths.KeysDirectory))
            yield return keyFile;
    }
}
