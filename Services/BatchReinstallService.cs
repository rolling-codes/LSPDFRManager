using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class BatchReinstallService
{
    private readonly InstallQueue _queue;
    private readonly ConfigManagerService _configManager;

    public BatchReinstallService()
    {
        _queue = InstallQueue.Instance;
        _configManager = ConfigManagerService.Instance;
    }

    public async Task<List<string>> ReinstallFromManifestAsync(string manifestPath, IProgress<string>? progress = null)
    {
        var issues = new List<string>();
        var (manifest, tempDirectory) = await LoadManifestAsync(manifestPath);

        try
        {
            progress?.Report($"Found {manifest.Mods.Count} mods to reinstall.");
            RestoreConfigSnapshots(manifest, progress);

            var detector = new ModDetector();

            foreach (var entry in manifest.Mods)
            {
                if (!File.Exists(entry.SourceArchivePath))
                {
                    issues.Add($"Skipping {entry.Name}: source archive not found at {entry.SourceArchivePath}");
                    continue;
                }

                progress?.Report($"Queuing {entry.Name}...");
                var modInfo = await Task.Run(() => detector.Detect(entry.SourceArchivePath));

                modInfo.Name = entry.Name;
                modInfo.Type = entry.Type;
                modInfo.TypeLabel = entry.TypeLabel;
                modInfo.Version = entry.Version;
                modInfo.Author = entry.Author;
                modInfo.DlcPackName = entry.DlcPackName;

                _queue.Enqueue(modInfo);
            }

            progress?.Report("All mods queued. Installation continues in background.");
            return issues;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private void RestoreConfigSnapshots(ModManifest manifest, IProgress<string>? progress)
    {
        foreach (var mod in manifest.Mods)
        {
            foreach (var snapshot in mod.Configs)
            {
                _configManager.AddBuiltInConfig(mod.Name, snapshot.FileName, snapshot.Content);
                progress?.Report($"Restored config: {snapshot.FileName} for {mod.Name}");
            }
        }
    }

    private static async Task<(ModManifest Manifest, string? TempDirectory)> LoadManifestAsync(string manifestPath)
    {
        if (manifestPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            ZipFile.ExtractToDirectory(manifestPath, tempDirectory);
            var manifestFile = Directory.GetFiles(tempDirectory, "manifest.json", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("ZIP does not contain manifest.json");

            var manifestJson = await File.ReadAllTextAsync(manifestFile);
            var manifest = JsonSerializer.Deserialize<ModManifest>(manifestJson)
                ?? throw new InvalidOperationException("Invalid manifest");

            foreach (var mod in manifest.Mods)
            {
                var extractedArchive = Path.Combine(tempDirectory, Path.GetFileName(mod.SourceArchivePath));
                if (File.Exists(extractedArchive))
                    mod.SourceArchivePath = extractedArchive;
            }

            return (manifest, tempDirectory);
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        return (
            JsonSerializer.Deserialize<ModManifest>(json) ?? throw new InvalidOperationException("Invalid manifest"),
            null);
    }
}
