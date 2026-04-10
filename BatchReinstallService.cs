using LSPDFRManager.Core;
using LSPDFRManager.Models;

namespace LSPDFRManager.Services;

public class BatchReinstallService
{
    private readonly InstallQueue _queue;
    private readonly ModLibraryService _library;
    private readonly ConfigManagerService _configManager;
    private readonly BackupService _backup;

    public BatchReinstallService()
    {
        _queue = new InstallQueue();
        _library = ModLibraryService.Instance;
        _configManager = ConfigManagerService.Instance;
        _backup = new BackupService();
    }

    /// <summary>
    /// Imports a manifest (JSON or ZIP) and reinstalls all mods into the current GTA V folder.
    /// </summary>
    public async Task<List<string>> ReinstallFromManifestAsync(string manifestPath, IProgress<string>? progress = null)
    {
        var issues = new List<string>();
        ModManifest manifest;

        // If it's a ZIP, extract manifest.json first
        if (manifestPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(manifestPath, tempDir);
            var jsonFile = Directory.GetFiles(tempDir, "manifest.json").FirstOrDefault();
            if (jsonFile == null)
            {
                throw new Exception("ZIP does not contain manifest.json");
            }
            var json = await File.ReadAllTextAsync(jsonFile);
            manifest = JsonSerializer.Deserialize<ModManifest>(json) ?? throw new Exception("Invalid manifest");
            // Optionally copy archives from ZIP to a temp folder for later use
            foreach (var mod in manifest.Mods)
            {
                var archiveInZip = Path.Combine(tempDir, Path.GetFileName(mod.SourceArchivePath));
                if (File.Exists(archiveInZip))
                    mod.SourceArchivePath = archiveInZip;
                else
                    issues.Add($"Missing archive for {mod.Name} in ZIP");
            }
        }
        else
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            manifest = JsonSerializer.Deserialize<ModManifest>(json) ?? throw new Exception("Invalid manifest");
        }

        progress?.Report($"Found {manifest.Mods.Count} mods to reinstall.");

        // First, restore config snapshots into ConfigManagerService
        foreach (var entry in manifest.Mods)
        {
            foreach (var cfgSnapshot in entry.Configs)
            {
                var existing = _configManager.Configs.FirstOrDefault(c => c.ModName == entry.Name && c.ConfigFileName == cfgSnapshot.FileName);
                if (existing != null)
                    _configManager.UpdateConfig(existing.Id, cfgSnapshot.Content);
                else
                    _configManager.AddBuiltInConfig(entry.Name, cfgSnapshot.FileName, cfgSnapshot.Content);
                progress?.Report($"Restored config: {cfgSnapshot.FileName} for {entry.Name}");
            }
        }

        // Now enqueue each mod for installation
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
            // Override detection with saved metadata
            modInfo.Name = entry.Name;
            modInfo.Type = entry.Type;
            modInfo.TypeLabel = entry.TypeLabel;
            modInfo.Version = entry.Version;
            modInfo.Author = entry.Author;
            modInfo.DlcPackName = entry.DlcPackName;
            _queue.Enqueue(modInfo);
        }

        // The queue processes asynchronously; we return immediately with issues list
        progress?.Report("All mods queued. Installation continues in background.");
        return issues;
    }
}