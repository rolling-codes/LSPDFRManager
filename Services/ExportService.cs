using LSPDFRManager.Domain;

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

    public async Task<string> ExportAsync(
        string outputPath,
        bool includeArchiveFiles = false,
        IProgress<string>? progress = null)
    {
        var manifest = BuildManifest(progress);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var tempManifestPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(tempManifestPath, json);

        try
        {
            if (!includeArchiveFiles)
            {
                File.Copy(tempManifestPath, outputPath, overwrite: true);
                progress?.Report($"Exported manifest to {outputPath}");
                return outputPath;
            }

            var zipPath = outputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? outputPath
                : outputPath + ".zip";

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(tempManifestPath, "manifest.json");

            foreach (var mod in manifest.Mods)
            {
                if (!File.Exists(mod.SourceArchivePath))
                {
                    progress?.Report($"Warning: missing archive {mod.SourceArchivePath}");
                    continue;
                }

                zip.CreateEntryFromFile(mod.SourceArchivePath, Path.GetFileName(mod.SourceArchivePath));
                progress?.Report($"Added archive: {Path.GetFileName(mod.SourceArchivePath)}");
            }

            progress?.Report($"Exported package to {zipPath}");
            return zipPath;
        }
        finally
        {
            if (File.Exists(tempManifestPath))
                File.Delete(tempManifestPath);
        }
    }

    private ModManifest BuildManifest(IProgress<string>? progress)
    {
        var manifest = new ModManifest();

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
                InstalledFiles = mod.InstalledFiles
                    .Select(file => GetRelativePath(file, AppConfig.Instance.GtaPath))
                    .ToList(),
                Configs = GetConfigSnapshots(mod),
            };

            manifest.Mods.Add(entry);
            progress?.Report($"Processed: {mod.Name}");
        }

        return manifest;
    }

    private List<ConfigSnapshot> GetConfigSnapshots(InstalledMod mod)
    {
        return _configManager.Configs
            .Where(config => config.ModName.Equals(mod.Name, StringComparison.OrdinalIgnoreCase))
            .Select(config => new ConfigSnapshot
            {
                FileName = config.ConfigFileName,
                Content = config.ConfigContent,
                RelativeInstallPath = GetRelativePath(
                    Path.Combine(mod.InstallPath, config.ConfigFileName),
                    AppConfig.Instance.GtaPath),
            })
            .ToList();
    }

    private static string GetRelativePath(string fullPath, string basePath) =>
        fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar)
            : fullPath;
}
