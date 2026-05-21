using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ConfigDiscoveryService
{
    private readonly string _gtaPath;

    public ConfigDiscoveryService(string gtaPath)
    {
        _gtaPath = gtaPath;
    }

    public List<DiscoveredConfig> DiscoverAll()
    {
        var results = new List<DiscoveredConfig>();

        if (string.IsNullOrWhiteSpace(_gtaPath) || !Directory.Exists(_gtaPath))
            return results;

        var patterns = new[]
        {
            (Path.Combine(_gtaPath, "plugins", "lspdfr"), "*.ini", true),
            (Path.Combine(_gtaPath, "plugins", "lspdfr"), "*.xml", true),
            (Path.Combine(_gtaPath, "ELS"), "*.xml", true),
            (Path.Combine(_gtaPath, "scripts"), "*.ini", true),
            (Path.Combine(_gtaPath, "scripts"), "*.xml", true),
        };

        foreach (var (dir, pattern, recurse) in patterns)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var file in Directory.GetFiles(dir, pattern, option))
                {
                    AddResult(results, file);
                }
            }
            catch (IOException ex)
            {
                AppLogger.Warning($"ConfigDiscovery: error scanning {dir}: {ex.Message}");
            }
        }

        var elsIni = Path.Combine(_gtaPath, "ELS.ini");
        if (File.Exists(elsIni))
            AddResult(results, elsIni);

        return [.. results
            .DistinctBy(r => r.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase)];
    }

    private void AddResult(List<DiscoveredConfig> results, string absolutePath)
    {
        try
        {
            var info = new FileInfo(absolutePath);
            var rel = Path.GetRelativePath(_gtaPath, absolutePath).Replace('\\', '/');
            var ext = Path.GetExtension(absolutePath).TrimStart('.').ToLowerInvariant();
            results.Add(new DiscoveredConfig
            {
                AbsolutePath = absolutePath,
                RelativePath = rel,
                FileType = ext,
                PluginOwner = InferOwner(rel),
                FileSizeBytes = info.Length,
                LastModified = info.LastWriteTime,
            });
        }
        catch (IOException ex)
        {
            AppLogger.Warning($"ConfigDiscovery: error reading file info for {absolutePath}: {ex.Message}");
        }
    }

    private static string? InferOwner(string relativePath)
    {
        // relativePath uses forward slashes
        const string prefix = "plugins/lspdfr/";
        if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = relativePath[prefix.Length..];
        var slash = remainder.IndexOf('/');
        if (slash <= 0)
            return null; // file is directly at lspdfr root level

        return remainder[..slash];
    }
}
