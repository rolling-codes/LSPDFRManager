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
        set => SetProperty(ref _selectedMod, value);
    }

    public int TotalMods => _library.Mods.Count;
    public int EnabledMods => _library.Mods.Count(m => m.IsEnabled);
    public bool IsEmpty => FilteredMods.Count == 0;

    public List<string> FilterOptions { get; } =
    [
        "All", "LSPDFR Plugin", "Vehicle Add-On DLC", "Vehicle Replace",
        "ASI Mod", "Script (CS/VB)", "EUP Clothing", "Map / MLO",
        "Sound Pack", "Miscellaneous",
    ];

    public ICommand ToggleEnabledCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand RefreshCommand { get; }

    public LibraryViewModel()
    {
        ToggleEnabledCommand = new RelayCommand(obj =>
        {
            if (obj is InstalledMod mod)
            {
                _library.SetEnabled(mod.Id, !mod.IsEnabled);
                RefreshFiltered();
                OnPropertyChanged(nameof(EnabledMods));
            }
        });

        UninstallCommand = new RelayCommand(obj =>
        {
            if (obj is InstalledMod mod)
            {
                // Attempt to remove installed files
                foreach (var file in mod.InstalledFiles)
                {
                    try
                    {
                        if (File.Exists(file)) File.Delete(file);
                        var disabled = file + ".disabled";
                        if (File.Exists(disabled)) File.Delete(disabled);
                    }
                    catch { /* log but don't crash */ }
                }
                _library.Remove(mod.Id);
                if (SelectedMod?.Id == mod.Id) SelectedMod = null;
                RefreshFiltered();
                OnPropertyChanged(nameof(TotalMods));
                OnPropertyChanged(nameof(EnabledMods));
            }
        });

        RefreshCommand = new RelayCommand(() =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
        });

        _library.Mods.CollectionChanged += (_, _) =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
        };

        RefreshFiltered();
    }

    private void RefreshFiltered()
    {
        FilteredMods.Clear();

        var query = _searchQuery.Trim();
        var mods = string.IsNullOrEmpty(query)
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
