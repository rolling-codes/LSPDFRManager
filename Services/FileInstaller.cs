using LSPDFRManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LSPDFRManager.Services;

/// <summary>
/// Extracts mod files from a <c>.zip</c>, <c>.rar</c>, <c>.7z</c> archive, or
/// plain directory into a target root folder (the GTA V directory).
/// </summary>
public static class FileInstaller
{
    /// <summary>
    /// Extracts all files from <paramref name="mod"/> into <paramref name="targetRoot"/>,
    /// overwriting any existing files.
    /// </summary>
    /// <param name="mod">Mod to install; <see cref="ModInfo.SourcePath"/> must exist.</param>
    /// <param name="targetRoot">Destination directory (GTA V root).</param>
    public static void Install(ModInfo mod, string targetRoot)
    {
        if (Directory.Exists(mod.SourcePath))
        {
            CopyDirectory(mod.SourcePath, targetRoot);
        }
        else if (mod.SourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(mod.SourcePath);
            var sharedRoot = GetSharedTopLevelDirectory(
                zip.Entries.Where(e => !e.FullName.EndsWith("/")).Select(e => e.FullName));
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/")) continue;
                var relative = NormalizeArchivePath(entry.FullName, sharedRoot);
                var dest = GetSafeDestinationPath(targetRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
        else // rar/7z via SharpCompress
        {
            using var archive = ArchiveFactory.Open(mod.SourcePath);
            var sharedRoot = GetSharedTopLevelDirectory(
                archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key ?? string.Empty));
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var relative = NormalizeArchivePath(entry.Key ?? string.Empty, sharedRoot);
                var dest = GetSafeDestinationPath(targetRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.WriteToFile(dest, new ExtractionOptions { Overwrite = true });
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var sharedRoot = GetSharedTopLevelDirectory(
            files.Select(f => Path.GetRelativePath(source, f).Replace('\\', '/')));

        foreach (var file in files)
        {
            var relPath = Path.GetRelativePath(source, file).Replace('\\', '/');
            relPath = NormalizeArchivePath(relPath, sharedRoot);
            var dest = GetSafeDestinationPath(target, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static string NormalizeArchivePath(string rawPath, string? sharedRoot)
    {
        var normalized = rawPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (sharedRoot is not null &&
            normalized.StartsWith(sharedRoot + "/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[(sharedRoot.Length + 1)..];

        return normalized;
    }

    private static string? GetSharedTopLevelDirectory(IEnumerable<string> rawPaths)
    {
        var segments = rawPaths
            .Select(p => p.Replace('\\', '/').TrimStart('/'))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length > 1)
            .Select(parts => parts[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return segments.Count == 1 ? segments[0] : null;
    }

    private static string GetSafeDestinationPath(string targetRoot, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        var destination = Path.GetFullPath(Path.Combine(fullTargetRoot, normalized));

        if (!destination.StartsWith(fullTargetRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe archive path blocked: {relativePath}");

        return destination;
    }
}
