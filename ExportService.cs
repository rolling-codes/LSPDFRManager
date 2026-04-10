using System.IO.Compression;
using System.Text.Json;
using LSPDFRManager.Core;
using LSPDFRManager.Models;

namespace LSPDFRManager.Services;

public class ExportService
{
    private readonly ModLibraryService _library;
    private readonly ConfigManagerService _configManager;

    public ExportService()
    {
        _library = ModLibraryService.Instance;
        _configManager = ConfigManagerService.Instance;
    }

    /// <summary>
    /// Exports all installed mods into a .lspmanifest file (JSON) and optionally packages them into a ZIP.
    /// </summary>
    public async Task<string> ExportAsync(string outputPath, bool includeArchiveFiles = false, IProgress<string>? progress = null)
    {
        var manifest = new ModManifest();
        progress?.Report("Scanning installed mods...");

        foreach (var mod in _library.Mods)
        {
            var entry = new ManifestEntry
            {
                OriginalId = mod.Id,
                Name = mod.Name,
                Type = mod.Type,
                TypeLabel = mod.TypeLabel,
                Version = mod.Version,
                Author = mod.Author,
                SourceArchivePath = mod.SourcePath,
                DlcPackName = mod.DlcPackName,
                IsEnabled = mod.IsEnabled,
                InstalledFiles = mod.InstalledFiles.Select(f => GetRelativePath(f, AppConfig.Instance.GtaPath)).ToList(),
                Configs = new List<ConfigSnapshot>()
            };

            // Capture current config files for this mod (from ConfigManagerService)
            var modConfigs = _configManager.Configs.Where(c => c.ModName.Equals(mod.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var cfg in modConfigs)
            {
                entry.Configs.Add(new ConfigSnapshot
                {
                    FileName = cfg.ConfigFileName,
                    Content = cfg.ConfigContent,
                    RelativeInstallPath = GetRelativePath(Path.Combine(mod.InstallPath, cfg.ConfigFileName), AppConfig.Instance.GtaPath)
                });
            }
            manifest.Mods.Add(entry);
            progress?.Report($"Processed: {mod.Name}");
        }

        // Save manifest as JSON
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var manifestPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(manifestPath, json);

        if (includeArchiveFiles)
        {
            // Create a ZIP containing the manifest + all original mod archives
            var zipPath = outputPath.EndsWith(".zip") ? outputPath : outputPath + ".zip";
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(manifestPath, "manifest.json");
                foreach (var mod in manifest.Mods)
                {
                    if (File.Exists(mod.SourceArchivePath))
                    {
                        zip.CreateEntryFromFile(mod.SourceArchivePath, Path.GetFileName(mod.SourceArchivePath));
                        progress?.Report($"Added archive: {Path.GetFileName(mod.SourceArchivePath)}");
                    }
                    else
                    {
                        progress?.Report($"Warning: missing archive {mod.SourceArchivePath}");
                    }
                }
            }
            File.Delete(manifestPath);
            return zipPath;
        }
        else
        {
            // Save only the manifest JSON
            File.Copy(manifestPath, outputPath, overwrite: true);
            File.Delete(manifestPath);
            return outputPath;
        }
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return fullPath;
        return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
    }
}