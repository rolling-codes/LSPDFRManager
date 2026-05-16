using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class OivSourceScanner : IOivSourceScanner
{
    // Extensions that must never be packaged — executable or script types.
    private static readonly HashSet<string> BlockedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".sh", ".msi", ".com", ".scr"
        };

    public OivPackagePlan Scan(IReadOnlyList<string> sourcePaths, OivPackagePlan template)
    {
        var files    = new List<OivPackageFile>();
        var warnings = new List<string>(template.Warnings);

        foreach (var path in sourcePaths)
        {
            if (File.Exists(path))
            {
                AddFile(path, Path.GetFileName(path), files, warnings);
            }
            else if (Directory.Exists(path))
            {
                foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(path, f).Replace('\\', '/');
                    AddFile(f, rel, files, warnings);
                }
            }
            else
            {
                warnings.Add($"Source not found: {path}");
            }
        }

        return template with { Files = files, Warnings = warnings };
    }

    private static void AddFile(
        string absolutePath,
        string installRelPath,
        List<OivPackageFile> files,
        List<string> warnings)
    {
        var ext = Path.GetExtension(absolutePath);
        if (BlockedExtensions.Contains(ext))
        {
            warnings.Add($"Skipped unsafe file type ({ext}): {Path.GetFileName(absolutePath)}");
            return;
        }

        try
        {
            var size = new FileInfo(absolutePath).Length;
            files.Add(new OivPackageFile(absolutePath, installRelPath, size));
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not read file info for {Path.GetFileName(absolutePath)}: {ex.Message}");
        }
    }
}
