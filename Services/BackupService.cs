using System.IO;
using System.IO.Compression;
using LSPDFRManager.Core;
using LSPDFRManager.Models;

namespace LSPDFRManager.Services;

public class BackupService
{
    /// <summary>Creates a ZIP backup of the mod library and config data.</summary>
    public async Task<string> CreateBackupAsync(IProgress<string>? progress = null)
    {
        var config = AppConfig.Instance;
        var backupDir = config.BackupPath;
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var backupPath = Path.Combine(backupDir, $"lspmanager_backup_{timestamp}.zip");

        progress?.Report("Creating backup...");

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LSPDFRManager");

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(backupPath, ZipArchiveMode.Create);

            foreach (var file in new[] { "library.json", "configs.json", "config.json" })
            {
                var fullPath = Path.Combine(appData, file);
                if (File.Exists(fullPath))
                {
                    zip.CreateEntryFromFile(fullPath, file);
                    progress?.Report($"Backed up: {file}");
                }
            }

            // Backup keys folder
            var keysDir = Path.Combine(appData, "keys");
            if (Directory.Exists(keysDir))
            {
                foreach (var keyFile in Directory.GetFiles(keysDir))
                {
                    zip.CreateEntryFromFile(keyFile, Path.Combine("keys", Path.GetFileName(keyFile)));
                }
                progress?.Report("Backed up keys.");
            }
        });

        config.LastBackupDate = DateTime.Now;
        config.Save();

        AppLogger.Info($"Backup created: {backupPath}");
        progress?.Report($"Done — {backupPath}");
        return backupPath;
    }

    /// <summary>Restores library and config data from a backup ZIP.</summary>
    public async Task RestoreFromBackupAsync(string backupPath, IProgress<string>? progress = null)
    {
        progress?.Report($"Restoring from {Path.GetFileName(backupPath)}...");

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LSPDFRManager");

        await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(backupPath);
            foreach (var entry in zip.Entries)
            {
                var dest = Path.Combine(appData, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
                progress?.Report($"Restored: {entry.FullName}");
            }
        });

        AppLogger.Info($"Restored from backup: {backupPath}");
        progress?.Report("Restore complete. Please restart the application.");
    }

    /// <summary>Returns all backup files sorted newest-first.</summary>
    public IEnumerable<string> ListBackups()
    {
        var dir = AppConfig.Instance.BackupPath;
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "lspmanager_backup_*.zip")
                        .OrderByDescending(f => f);
    }
}
