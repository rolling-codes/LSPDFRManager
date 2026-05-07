using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class StorageUsageAnalyzer
{
    public List<StorageUsageResult> Analyze()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var appData = AppDataPaths.Root;
        var results = new List<StorageUsageResult>();

        var folders = new (string Label, string Path)[]
        {
            ("Plugins", Path.Combine(gtaPath, "plugins")),
            ("LSPDFR", Path.Combine(gtaPath, "plugins", "lspdfr")),
            ("Scripts", Path.Combine(gtaPath, "scripts")),
            ("Mods", Path.Combine(gtaPath, "mods")),
            ("ELS", Path.Combine(gtaPath, "ELS")),
            ("Backups", AppConfig.Instance.BackupPath),
            ("Restore Points", AppDataPaths.RestorePointsDirectory),
            ("Profiles", AppDataPaths.ProfilesDirectory),
            ("Logs", Path.Combine(appData, "logs")),
            ("App Data", appData),
        };

        foreach (var (label, path) in folders)
        {
            if (!Directory.Exists(path)) continue;
            try
            {
                var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
                var size = files.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
                results.Add(new StorageUsageResult { Label = label, FolderPath = path, SizeBytes = size, FileCount = files.Count });
            }
            catch { }
        }

        return results;
    }
}
