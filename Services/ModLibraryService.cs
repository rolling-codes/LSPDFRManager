using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModLibraryService
{
    private static readonly JsonFileStore<List<InstalledMod>> Store = new(AppDataPaths.LibraryFile);
    private static ModLibraryService? _instance;

    private readonly InstalledModFileService _fileService = new();

    public static ModLibraryService Instance => _instance ??= new ModLibraryService();

    public ObservableCollection<InstalledMod> Mods { get; } = [];
    public event Action<InstalledMod>? ModUpdated;

    public ModLibraryService() => Load();

    public void Add(InstalledMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        UiDispatcher.Invoke(() => Mods.Add(mod));
        Save();
    }

    public void Remove(Guid id)
    {
        UiDispatcher.Invoke(() =>
        {
            var mod = Mods.FirstOrDefault(item => item.Id == id);
            if (mod is not null)
                Mods.Remove(mod);
        });

        Save();
    }

    public void SaveProxy() => Save();

    public void SetEnabled(Guid id, bool enabled)
    {
        InstalledMod? target = null;

        UiDispatcher.Invoke(() =>
        {
            target = Mods.FirstOrDefault(mod => mod.Id == id);
        });

        if (target is null || target.IsEnabled == enabled)
            return;

        _fileService.SetEnabled(target, enabled);
        ModUpdated?.Invoke(target);
        Save();
    }

    public IEnumerable<InstalledMod> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Mods;

        return Mods.Where(mod =>
            mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            mod.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            mod.Author.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsDlcPackInstalled(string dlcName) =>
        !string.IsNullOrWhiteSpace(dlcName) &&
        Mods.Any(mod => mod.DlcPackName.Equals(dlcName, StringComparison.OrdinalIgnoreCase));

    public List<string> FindConflicts(InstalledMod candidate) => _fileService.FindConflicts(Mods, candidate);

    public void Uninstall(Guid id)
    {
        InstalledMod? target = null;

        UiDispatcher.Invoke(() =>
        {
            target = Mods.FirstOrDefault(mod => mod.Id == id);
        });

        if (target is null)
            return;

        _fileService.Uninstall(target, Mods);
        Remove(id);
    }

    private void Load()
    {
        var items = Store.LoadOrDefault(static () => []);
        UiDispatcher.Invoke(() =>
        {
            Mods.Clear();
            foreach (var item in items.OrderByDescending(mod => mod.InstalledAt))
                Mods.Add(item);
        });
    }

    private void Save() => Store.Save(Mods.ToList());
}
