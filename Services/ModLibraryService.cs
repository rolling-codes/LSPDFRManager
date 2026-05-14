using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModLibraryService
{
    private static readonly JsonFileStore<List<InstalledMod>> Store = new(AppDataPaths.LibraryFile);
    private static ModLibraryService? _instance;

    private readonly InstalledModFileService _fileService = new();
    private readonly object _mutationLock = new();

    public static ModLibraryService Instance => _instance ??= new ModLibraryService();

    public ObservableCollection<InstalledMod> Mods { get; } = [];
    public event Action<InstalledMod>? ModUpdated;

    public ModLibraryService() => Load();

    public void Add(InstalledMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        lock (_mutationLock)
        {
            if (mod.LoadOrderPriority <= 0)
                mod.LoadOrderPriority = NextLoadOrderPriority();

            UiDispatcher.Invoke(() => Mods.Add(mod));
            Save();
        }
    }

    public void Remove(Guid id)
    {
        lock (_mutationLock)
        {
            UiDispatcher.Invoke(() =>
            {
                var mod = Mods.FirstOrDefault(item => item.Id == id);
                if (mod is not null)
                    Mods.Remove(mod);
            });

            Save();
        }
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

        lock (_mutationLock)
        {
            _fileService.SetEnabled(target, enabled);
            ModUpdated?.Invoke(target);
            Save();
        }
    }

    public void SetEnabledBatch(IEnumerable<Guid> ids, bool enabled)
    {
        var idSet = ids.ToHashSet();
        List<InstalledMod> targets = [];

        UiDispatcher.Invoke(() =>
        {
            targets = Mods.Where(mod => idSet.Contains(mod.Id) && mod.IsEnabled != enabled).ToList();
        });

        lock (_mutationLock)
        {
            foreach (var target in targets)
            {
                _fileService.SetEnabled(target, enabled);
                ModUpdated?.Invoke(target);
            }

            if (targets.Count > 0)
                Save();
        }
    }

    public void SetLoadOrder(Guid id, int priority)
    {
        lock (_mutationLock)
        {
            var target = Mods.FirstOrDefault(mod => mod.Id == id);
            if (target is null)
                return;

            target.LoadOrderPriority = priority;
            ModUpdated?.Invoke(target);
            Save();
        }
    }

    public void Reorder(Guid id, int direction)
    {
        lock (_mutationLock)
        {
            var ordered = Mods
                .OrderBy(mod => mod.LoadOrderPriority == 0 ? int.MaxValue : mod.LoadOrderPriority)
                .ThenBy(mod => mod.InstalledAt)
                .ToList();
            var index = ordered.FindIndex(mod => mod.Id == id);
            var swapIndex = index + direction;

            if (index < 0 || swapIndex < 0 || swapIndex >= ordered.Count)
                return;

            (ordered[index].LoadOrderPriority, ordered[swapIndex].LoadOrderPriority) =
                (NormalizePriority(ordered[swapIndex], swapIndex), NormalizePriority(ordered[index], index));

            Save();
        }
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

        lock (_mutationLock)
        {
            _fileService.Uninstall(target, Mods);
            Remove(id);
        }
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

    private int NextLoadOrderPriority() =>
        Mods.Count == 0 ? 1 : Mods.Max(mod => mod.LoadOrderPriority) + 1;

    private static int NormalizePriority(InstalledMod mod, int fallbackIndex) =>
        mod.LoadOrderPriority == 0 ? fallbackIndex + 1 : mod.LoadOrderPriority;

    private void Save() => Store.Save(Mods.ToList());
}
