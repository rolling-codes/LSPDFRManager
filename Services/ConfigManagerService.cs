using LSPDFRManager.Core;

namespace LSPDFRManager.Services;

/// <summary>A captured mod config file stored by <see cref="ConfigManagerService"/>.</summary>
public class ConfigEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ModName { get; set; } = "";
    public string ConfigFileName { get; set; } = "";
    public string ConfigContent { get; set; } = "";
    public DateTime LastModified { get; set; } = DateTime.Now;
}

/// <summary>
/// Stores and retrieves mod config file snapshots so they can be restored when
/// reinstalling from a <see cref="LSPDFRManager.Models.ModManifest"/>.
/// Persisted to <c>%APPDATA%\LSPDFRManager\configs.json</c>.
/// </summary>
public class ConfigManagerService
{
    private static readonly string ConfigsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "configs.json");

    private static ConfigManagerService? _instance;
    public static ConfigManagerService Instance => _instance ??= new ConfigManagerService();

    public ObservableCollection<ConfigEntry> Configs { get; } = [];

    private ConfigManagerService() => Load();

    public void AddBuiltInConfig(string modName, string fileName, string content)
    {
        var entry = new ConfigEntry
        {
            ModName = modName,
            ConfigFileName = fileName,
            ConfigContent = content,
        };
        Configs.Add(entry);
        Save();
        AppLogger.Info($"Config added: {fileName} for {modName}");
    }

    public void UpdateConfig(Guid id, string content)
    {
        var entry = Configs.FirstOrDefault(c => c.Id == id);
        if (entry is null) return;
        entry.ConfigContent = content;
        entry.LastModified = DateTime.Now;
        Save();
    }

    public void RemoveConfig(Guid id)
    {
        var entry = Configs.FirstOrDefault(c => c.Id == id);
        if (entry is null) return;
        Configs.Remove(entry);
        Save();
    }

    private void Load()
    {
        if (!File.Exists(ConfigsPath)) return;
        try
        {
            var json = File.ReadAllText(ConfigsPath);
            var list = JsonSerializer.Deserialize<List<ConfigEntry>>(json);
            if (list is null) return;
            foreach (var e in list) Configs.Add(e);
        }
        catch (Exception ex) { AppLogger.Warning($"Configs load failed: {ex.Message}"); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigsPath)!);
            var json = JsonSerializer.Serialize(Configs.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigsPath, json);
        }
        catch (Exception ex) { AppLogger.Warning($"Configs save failed: {ex.Message}"); }
    }
}
