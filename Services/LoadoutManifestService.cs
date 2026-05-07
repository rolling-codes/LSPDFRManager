using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class LoadoutManifestService
{
    public LoadoutManifest Export()
    {
        var mods = ModLibraryService.Instance.Mods;
        var gameVersion = new GameVersionService().GetCurrentVersion();

        return new LoadoutManifest
        {
            GameVersion = gameVersion.Version,
            EnabledMods = mods.Where(m => m.IsEnabled).Select(m => m.Name).ToList(),
            DisabledMods = mods.Where(m => !m.IsEnabled).Select(m => m.Name).ToList(),
        };
    }

    public async Task ExportToFileAsync(string outputPath)
    {
        var manifest = Export();
        var json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);
    }

    public async Task<LoadoutManifest?> ImportFromFileAsync(string inputPath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(inputPath);
            return System.Text.Json.JsonSerializer.Deserialize<LoadoutManifest>(json);
        }
        catch { return null; }
    }

    public (List<string> Missing, List<string> Extra) Compare(LoadoutManifest imported)
    {
        var current = ModLibraryService.Instance.Mods.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedAll = imported.EnabledMods.Concat(imported.DisabledMods).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = importedAll.Where(n => !current.Contains(n)).ToList();
        var extra = current.Where(n => !importedAll.Contains(n)).ToList();
        return (missing, extra);
    }
}
