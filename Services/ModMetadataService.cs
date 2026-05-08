using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModMetadataService
{
    private static ModMetadataService? _instance;
    public static ModMetadataService Instance => _instance ??= new();

    private Dictionary<string, ModMetadata> _metadata = [];

    public void Load()
    {
        var path = AppDataPaths.ModMetadataFile;
        if (!File.Exists(path)) { _metadata = []; return; }
        try
        {
            var json = File.ReadAllText(path);
            _metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ModMetadata>>(json) ?? [];
        }
        catch { _metadata = []; }
    }

    public ModMetadata GetOrCreate(string modId)
    {
        if (!_metadata.TryGetValue(modId, out var meta))
        {
            meta = new ModMetadata { ModId = modId };
            _metadata[modId] = meta;
        }
        return meta;
    }

    public void Save(ModMetadata meta)
    {
        _metadata[meta.ModId] = meta;
        Persist();
    }

    public void Delete(string modId)
    {
        _metadata.Remove(modId);
        Persist();
    }

    private void Persist()
    {
        var path = AppDataPaths.ModMetadataFile;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(_metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
