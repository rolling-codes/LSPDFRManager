namespace LSPDFRManager.Services;

public class BackupConfigDiscoveryService
{
    private readonly string _gtaPath;

    public BackupConfigDiscoveryService(string gtaPath) => _gtaPath = gtaPath;

    public List<string> DiscoverBackupXmlFiles()
    {
        if (string.IsNullOrWhiteSpace(_gtaPath) || !Directory.Exists(_gtaPath))
            return [];

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Scan(string pattern)
        {
            try
            {
                // Split pattern into base dir and file glob
                var normalized = pattern.Replace('/', Path.DirectorySeparatorChar);
                var parts = normalized.Split(Path.DirectorySeparatorChar);
                // Find the first wildcard segment
                int wildIdx = Array.FindIndex(parts, p => p.Contains('*') || p.Contains('?'));
                if (wildIdx < 0)
                {
                    // Direct file check
                    var full = Path.Combine(_gtaPath, normalized);
                    if (File.Exists(full)) results.Add(full);
                    return;
                }

                var baseDir = Path.Combine([_gtaPath, .. parts[..wildIdx]]);
                if (!Directory.Exists(baseDir)) return;

                // Remaining is subdir glob + file pattern
                var remaining = string.Join(Path.DirectorySeparatorChar.ToString(), parts[wildIdx..]);
                var option = remaining.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var filePattern = parts[^1];

                foreach (var f in Directory.GetFiles(baseDir, filePattern, option))
                    results.Add(f);
            }
            catch (IOException ex)
            {
                Core.AppLogger.Warning($"BackupConfigDiscoveryService: scan error for pattern '{pattern}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Core.AppLogger.Warning($"BackupConfigDiscoveryService: access denied for pattern '{pattern}': {ex.Message}");
            }
        }

        // Ultimate Backup — both casing variants
        Scan("plugins/lspdfr/UltimateBackup/*.xml");
        Scan("plugins/lspdfr/Ultimate Backup/*.xml");
        Scan("Plugins/LSPDFR/UltimateBackup/*.xml");
        Scan("Plugins/LSPDFR/Ultimate Backup/*.xml");
        Scan("plugins/lspdfr/**/UltimateBackup*.xml");
        Scan("plugins/lspdfr/**/backup*.xml");
        Scan("plugins/lspdfr/**/regions*.xml");
        Scan("plugins/lspdfr/**/customregions*.xml");
        Scan("plugins/lspdfr/**/agency*.xml");
        Scan("plugins/lspdfr/**/units*.xml");
        Scan("plugins/lspdfr/**/special*.xml");

        // lspdfr/data and custom subdirectory (community packs often use these)
        Scan("plugins/lspdfr/data/backup.xml");
        Scan("lspdfr/data/backup.xml");
        Scan("lspdfr/data/**/*.xml");
        Scan("lspdfr/data/custom/**/*.xml");
        Scan("LSPDFR/data/**/*.xml");
        Scan("LSPDFR/data/custom/**/*.xml");

        return [.. results.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }
}
