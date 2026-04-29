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

    public void SaveProxy() => Save();

    public void SetEnabled(Guid id, bool enabled)
    {
        InstalledMod? target = null;

        UiDispatcher.Invoke(() =>
        {
            target = Mods.FirstOrDefault(m => m.Id == id);
            if (target is null || target.IsEnabled == enabled) return;

            target.IsEnabled = enabled;
            ModUpdated?.Invoke(target);
        });

        if (target is null) return;

        foreach (var file in target.InstalledFiles)
        {
            try
            {
                if (enabled)
                {
                    var disabledPath = file + ".disabled";
                    if (File.Exists(disabledPath) && !File.Exists(file))
                        File.Move(disabledPath, file);
                }
                else
                {
                    if (File.Exists(file))
                        File.Move(file, file + ".disabled");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Toggle {file}: {ex.Message}");
            }
        }

        Save();
    }

    public IEnumerable<InstalledMod> Search(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? Mods
            : Mods.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (m.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

    public bool IsDlcPackInstalled(string dlcName) =>
        Mods.Any(m => m.DlcPackName.Equals(dlcName, StringComparison.OrdinalIgnoreCase));

    public List<string> FindConflicts(InstalledMod candidate)
    {
        var issues = new List<string>();

        if (!string.IsNullOrEmpty(candidate.DlcPackName) &&
            Mods.Any(m => m.Id != candidate.Id &&
                          m.DlcPackName.Equals(candidate.DlcPackName, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add($"DLC pack name '{candidate.DlcPackName}' is already installed");
        }

        var allFiles = Mods
            .Where(m => m.Id != candidate.Id)
            .SelectMany(m => m.InstalledFiles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in candidate.InstalledFiles)
        {
            if (allFiles.Contains(file))
                issues.Add($"File conflict: {Path.GetFileName(file)}");
        }

        return issues;
    }

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
