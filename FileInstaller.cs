using LSPDFRManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LSPDFRManager.Services;

public static class FileInstaller
{
    public static void Install(ModInfo mod, string targetRoot)
    {
        if (Directory.Exists(mod.SourcePath))
        {
            CopyDirectory(mod.SourcePath, targetRoot);
        }
        else if (mod.SourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(mod.SourcePath);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/")) continue;
                var dest = Path.Combine(targetRoot, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
        else // rar/7z via SharpCompress
        {
            using var archive = ArchiveFactory.Open(mod.SourcePath);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var dest = Path.Combine(targetRoot, entry.Key!);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.WriteToFile(dest, new ExtractionOptions { Overwrite = true });
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(source, file);
            var dest = Path.Combine(target, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}