using LSPDFRManager.Core;

namespace LSPDFRManager.Services;

public class ConfigEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ModName { get; set; } = "";
    public string ConfigFileName { get; set; } = "";
    public string ConfigContent { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public DateTime LastModified { get; set; } = DateTime.Now;
}

public class ConfigManagerService
{
    private static ConfigManagerService? _instance;
    private static readonly JsonFileStore<List<ConfigEntry>> Store = new(AppDataPaths.ConfigSnapshotsFile);

    public static ConfigManagerService Instance => _instance ??= new ConfigManagerService();

    public ObservableCollection<ConfigEntry> Configs { get; } = [];

    private ConfigManagerService() => Load();

    public void AddBuiltInConfig(string modName, string fileName, string content, string sourcePath = "")
    {
        var existing = FindBySource(sourcePath) ?? FindByName(modName, fileName);
        if (existing is not null)
        {
            existing.ConfigContent = content;
            existing.SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? existing.SourcePath : sourcePath;
            existing.LastModified = DateTime.Now;
            Save();
            return;
        }

        Configs.Add(new ConfigEntry
        {
            ModName = modName,
            ConfigFileName = fileName,
            ConfigContent = content,
            SourcePath = sourcePath,
            LastModified = DateTime.Now,
        });

        Save();
        AppLogger.Info($"Config added: {fileName} for {modName}");
    }

    public void UpdateConfig(Guid id, string content)
    {
        var entry = Configs.FirstOrDefault(config => config.Id == id);
        if (entry is null)
            return;

        entry.ConfigContent = content;
        entry.LastModified = DateTime.Now;
        Save();
    }

    public void RemoveConfig(Guid id)
    {
        var entry = Configs.FirstOrDefault(config => config.Id == id);
        if (entry is null)
            return;

        Configs.Remove(entry);
        Save();
    }

    private ConfigEntry? FindBySource(string sourcePath) =>
        string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : Configs.FirstOrDefault(config =>
                config.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));

    private ConfigEntry? FindByName(string modName, string fileName) =>
        Configs.FirstOrDefault(config =>
            config.ModName.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
            config.ConfigFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private void Load()
    {
        var items = Store.LoadOrDefault(static () => []);
        foreach (var item in items.OrderBy(item => item.ModName).ThenBy(item => item.ConfigFileName))
            Configs.Add(item);
    }

    private void Save() => Store.Save(Configs.ToList());
}
