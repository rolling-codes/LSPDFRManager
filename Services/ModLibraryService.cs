using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModLibraryService
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "library.json");

    public ObservableCollection<InstalledMod> Mods { get; } = [];

    private static ModLibraryService? _instance;
    public static ModLibraryService Instance => _instance ??= new ModLibraryService();

    public event Action<InstalledMod>? ModUpdated;

    public ModLibraryService() => Load();

    public void Add(InstalledMod mod)
    {
        UiDispatcher.Invoke(() => Mods.Add(mod));
        Save();
    }

    public void Remove(Guid id)
    {
        UiDispatcher.Invoke(() =>
        {
            var mod = Mods.FirstOrDefault(m => m.Id == id);
            if (mod is not null) Mods.Remove(mod);
        });
        Save();
    }

    public void SetEnabled(Guid id, bool enabled)
    {
        UiDispatcher.Invoke(() =>
        {
            var mod = Mods.FirstOrDefault(m => m.Id == id);
            if (mod is null) return;

            if (mod.IsEnabled == enabled) return;

            mod.IsEnabled = enabled;
            ModUpdated?.Invoke(mod);
        });

        Save();
    }

    public IEnumerable<InstalledMod> Search(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? Mods
            : Mods.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Author.Contains(query, StringComparison.OrdinalIgnoreCase));

    private void Load()
    {
        if (!File.Exists(LibraryPath)) return;
        try
        {
            var json = File.ReadAllText(LibraryPath);
            var list = JsonSerializer.Deserialize<List<InstalledMod>>(json);
            if (list is null) return;

            UiDispatcher.Invoke(() =>
            {
                foreach (var m in list) Mods.Add(m);
            });
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
            var json = JsonSerializer.Serialize(Mods.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LibraryPath, json);
        }
        catch { }
    }
}
