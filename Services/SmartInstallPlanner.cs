using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class SmartInstallPlanner
{
    private readonly ModDetector _detector = new();

    public InstallPlan BuildPlan(string archivePath, bool dryRun = false)
    {
        var modInfo = _detector.Detect(archivePath);
        var gtaPath = AppConfig.Instance.GtaPath;
        var entries = new List<InstallPlanEntry>();
        var warnings = new List<string>();
        string? readmeContent = null;

        try
        {
            using var archive = SharpCompress.Archives.ArchiveFactory.Open(archivePath) as SharpCompress.Archives.IArchive;
            if (archive is not null)
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var entryPath = entry.Key?.Replace('/', '\\') ?? "";
                    var targetPath = Path.Combine(gtaPath, entryPath);
                    var willOverwrite = File.Exists(targetPath);
                    var fileName = Path.GetFileName(entryPath).ToLowerInvariant();

                    var risk = InstallRisk.Safe;
                    if (willOverwrite) risk = InstallRisk.Overwrite;
                    if (fileName.EndsWith(".exe")) { risk = InstallRisk.Suspicious; warnings.Add($"Archive contains executable: {fileName}"); }
                    if (entryPath.Contains("..")) { risk = InstallRisk.Incompatible; warnings.Add($"Suspicious path: {entryPath}"); }

                    if (fileName is "readme.txt" or "readme.md" or "install.txt")
                    {
                        try { readmeContent = entry.OpenEntryStream().ReadToEnd(); } catch { }
                    }

                    entries.Add(new InstallPlanEntry
                    {
                        ArchivePath = entryPath,
                        TargetPath = targetPath,
                        WillOverwrite = willOverwrite,
                        Risk = risk,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not read archive: {ex.Message}");
        }

        if (entries.Any(e => e.WillOverwrite))
            warnings.Add($"{entries.Count(e => e.WillOverwrite)} file(s) will be overwritten.");

        return new InstallPlan
        {
            ArchiveSource = archivePath,
            DetectedType = modInfo.Type,
            Confidence = modInfo.Confidence,
            Entries = entries,
            Warnings = warnings,
            ReadmeContent = readmeContent,
            IsDryRun = dryRun,
        };
    }
}

internal static class StreamExtensions
{
    internal static string ReadToEnd(this Stream stream)
    {
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
