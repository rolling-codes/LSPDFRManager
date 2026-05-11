using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class PluginHealthScanner
{
    private static readonly string[] ScanSubFolders =
        ["plugins", "plugins/lspdfr", "lspdfr", "scripts", "mods", "ELS", "pack_default"];

    private static readonly string[] PluginExtensions =
        [".dll", ".asi", ".ini", ".xml", ".json", ".cs", ".vb", ".lua", ".rpf", ".ytd", ".yft", ".meta"];

    public List<PluginScanResult> Scan()
    {
        var results = new List<PluginScanResult>();
        var gtaPath = AppConfig.Instance.GtaPath;

        if (!Directory.Exists(gtaPath))
        {
            results.Add(new PluginScanResult
            {
                FilePath = gtaPath,
                FileName = "GTA V folder",
                Issue = "GTA V installation folder not found.",
                Severity = PluginScanSeverity.Error,
                RecommendedFix = "Set GTA V path in Settings.",
            });
            return results;
        }

        var seenFileNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sub in ScanSubFolders)
        {
            var dir = Path.Combine(gtaPath, sub);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();

                // Archive left inside game folder — check before extension filter
                if (ext is ".zip" or ".rar" or ".7z")
                {
                    results.Add(new PluginScanResult { FilePath = file, FileName = Path.GetFileName(file), Issue = "Mod archive found inside GTA V folder.", Severity = PluginScanSeverity.Warning, RecommendedFix = "Remove the archive after installation." });
                    continue;
                }

                // Disabled files use double extension (.dll.disabled) — handle before filter
                if (file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new PluginScanResult { FilePath = file, FileName = Path.GetFileName(file), Issue = "File is disabled (.disabled suffix).", Severity = PluginScanSeverity.Info });
                    var baseName2 = Path.GetFileName(file).Replace(".disabled", "", StringComparison.OrdinalIgnoreCase);
                    if (!seenFileNames.TryGetValue(baseName2, out var dp)) seenFileNames[baseName2] = dp = [];
                    dp.Add(file);
                    continue;
                }

                if (!PluginExtensions.Contains(ext)) continue;

                var name = Path.GetFileName(file);
                var info = new FileInfo(file);

                // Zero-byte file
                if (info.Length == 0)
                    results.Add(new PluginScanResult { FilePath = file, FileName = name, Issue = "Zero-byte file.", Severity = PluginScanSeverity.Warning, RecommendedFix = "Delete or reinstall." });

                // Old backup / copy naming
                if (name.Contains("copy", StringComparison.OrdinalIgnoreCase) || name.Contains(" - Copy", StringComparison.OrdinalIgnoreCase))
                    results.Add(new PluginScanResult { FilePath = file, FileName = name, Issue = "Possible duplicate/backup copy.", Severity = PluginScanSeverity.Warning, RecommendedFix = "Remove if not needed." });

                // Misplaced README
                if (name.Equals("readme.txt", StringComparison.OrdinalIgnoreCase) || name.Equals("readme.md", StringComparison.OrdinalIgnoreCase))
                    results.Add(new PluginScanResult { FilePath = file, FileName = name, Issue = "README file inside game folder.", Severity = PluginScanSeverity.Info, RecommendedFix = "Can be safely removed." });

                // Track duplicates by base name (without .disabled)
                var baseName = name.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase);
                if (!seenFileNames.TryGetValue(baseName, out var paths))
                    seenFileNames[baseName] = paths = [];
                paths.Add(file);
            }
        }

        // Duplicates
        foreach (var (baseName, paths) in seenFileNames.Where(kv => kv.Value.Count > 1))
        {
            results.Add(new PluginScanResult
            {
                FilePath = string.Join("; ", paths),
                FileName = baseName,
                Issue = $"Duplicate plugin detected ({paths.Count} copies).",
                Severity = PluginScanSeverity.Warning,
                RecommendedFix = "Keep only one copy.",
            });
        }

        if (results.Count == 0)
            results.Add(new PluginScanResult { FilePath = gtaPath, FileName = "All scanned folders", Issue = "No issues found.", Severity = PluginScanSeverity.Ok });

        return results;
    }
}
