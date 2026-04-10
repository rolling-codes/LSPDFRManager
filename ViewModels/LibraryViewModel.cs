using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class LibraryViewModel : ObservableObject
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;
    private string _searchQuery = "";
    private string _selectedFilter = "All";
    private InstalledMod? _selectedMod;

    public ObservableCollection<InstalledMod> FilteredMods { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set { SetProperty(ref _searchQuery, value); RefreshFiltered(); }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set { SetProperty(ref _selectedFilter, value); RefreshFiltered(); }
    }

    public InstalledMod? SelectedMod
    {
        get => _selectedMod;
        set
        {
            SetProperty(ref _selectedMod, value);
            OnPropertyChanged(nameof(HasSelectedMod));
            OnPropertyChanged(nameof(HasNoSelectedMod));
            OnPropertyChanged(nameof(SelectedModFilesSample));
            OnPropertyChanged(nameof(HasMoreSelectedFiles));
            OnPropertyChanged(nameof(SelectedModFilesMoreText));
        }
    }

    // ── Counts ───────────────────────────────────────────────────────
    public int TotalMods    => _library.Mods.Count;
    public int EnabledMods  => _library.Mods.Count(m => m.IsEnabled);
    public bool IsEmpty     => FilteredMods.Count == 0;

    // ── Selection detail ─────────────────────────────────────────────
    public bool HasSelectedMod  => _selectedMod is not null;
    public bool HasNoSelectedMod => _selectedMod is null;

    private const int FileSampleSize = 10;

    public IEnumerable<string> SelectedModFilesSample =>
        _selectedMod?.InstalledFiles
            .Select(f => Path.GetFileName(f) is { Length: > 0 } n ? n : f)
            .Take(FileSampleSize) ?? [];

    public bool HasMoreSelectedFiles =>
        (_selectedMod?.InstalledFiles.Count ?? 0) > FileSampleSize;

    public string SelectedModFilesMoreText =>
        HasMoreSelectedFiles
            ? $"+{_selectedMod!.InstalledFiles.Count - FileSampleSize} more files"
            : "";

    // ── Filter options ───────────────────────────────────────────────
    public List<string> FilterOptions { get; } =
    [
        "All", "LSPDFR Plugin", "Vehicle Add-On DLC", "Vehicle Replace",
        "ASI Mod", "Script (CS/VB)", "EUP Clothing", "Map / MLO",
        "Sound Pack", "Miscellaneous",
    ];

    // ── Commands ─────────────────────────────────────────────────────
    public ICommand ToggleEnabledCommand { get; }
    public ICommand UninstallCommand     { get; }
    public ICommand RefreshCommand       { get; }
    public ICommand OpenModFolderCommand { get; }

    public LibraryViewModel()
    {
        ToggleEnabledCommand = new RelayCommand(obj =>
        {
            if (obj is not InstalledMod mod) return;
            var enable = !mod.IsEnabled;
            SetModFilesEnabled(mod, enable);
            _library.SetEnabled(mod.Id, enable);
            RefreshFiltered();
            OnPropertyChanged(nameof(EnabledMods));
        });

        UninstallCommand = new RelayCommand(obj =>
        {
            if (obj is not InstalledMod mod) return;

            // Re-enable files so they exist under their original names before deletion
            if (!mod.IsEnabled) SetModFilesEnabled(mod, true);

            foreach (var file in mod.InstalledFiles)
            {
                try
                {
                    if (File.Exists(file))         File.Delete(file);
                    if (File.Exists(file + ".disabled")) File.Delete(file + ".disabled");
                }
                catch { /* best-effort */ }
            }

            // Remove DLC entry from dlclist.xml if applicable
            if (mod.Type == ModType.VehicleDlc && !string.IsNullOrEmpty(mod.DlcPackName))
                DlcListService.RemoveEntry(mod.DlcPackName);

            _library.Remove(mod.Id);
            if (SelectedMod?.Id == mod.Id) SelectedMod = null;
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
        });

        RefreshCommand = new RelayCommand(() =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
        });

        OpenModFolderCommand = new RelayCommand(obj =>
        {
            var path = obj is InstalledMod m ? m.InstallPath : _selectedMod?.InstallPath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Process.Start("explorer.exe", path);
        });

        _library.Mods.CollectionChanged += (_, _) =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
        };

        RefreshFiltered();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Renames each installed file to/from its <c>.disabled</c> variant so
    /// LSPDFR and ScriptHookV actually skip or load the plugin on next launch.
    /// </summary>
    private static void SetModFilesEnabled(InstalledMod mod, bool enable)
    {
        foreach (var file in mod.InstalledFiles)
        {
            try
            {
                var disabledPath = file + ".disabled";
                if (enable && File.Exists(disabledPath) && !File.Exists(file))
                    File.Move(disabledPath, file);
                else if (!enable && File.Exists(file))
                    File.Move(file, disabledPath);
            }
            catch { /* best-effort */ }
        }
    }

    private void RefreshFiltered()
    {
        FilteredMods.Clear();

        var query = _searchQuery.Trim();
        var mods  = string.IsNullOrEmpty(query)
            ? _library.Mods.AsEnumerable()
            : _library.Search(query);

        if (_selectedFilter != "All")
            mods = mods.Where(m =>
                m.TypeLabel.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var mod in mods)
            FilteredMods.Add(mod);

        OnPropertyChanged(nameof(IsEmpty));
    }
}
