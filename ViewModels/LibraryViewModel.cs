using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class LibraryViewModel : ObservableObject
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;
    private string _searchQuery = "";
    private string _selectedFilter = "All";
    private string _selectedSort = "Installed: Newest first";
    private string _riskFilter = "All";
    private ModItemViewModel? _selectedMod;

    public ObservableCollection<ModItemViewModel> FilteredMods { get; } = [];

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

    public string SelectedSort
    {
        get => _selectedSort;
        set { SetProperty(ref _selectedSort, value); RefreshFiltered(); }
    }

    public ModItemViewModel? SelectedMod
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
    public int DisabledMods => _library.Mods.Count(m => !m.IsEnabled);
    public int FilteredCount => FilteredMods.Count;
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

    public List<string> SortOptions { get; } =
    [
        "Installed: Newest first",
        "Installed: Oldest first",
        "Name: A to Z",
        "Name: Z to A",
        "Author: A to Z",
        "Enabled first",
    ];

    // ── Commands ─────────────────────────────────────────────────────
    public ICommand ToggleEnabledCommand { get; }
    public ICommand UninstallCommand     { get; }
    public ICommand RefreshCommand       { get; }
    public ICommand OpenModFolderCommand { get; }
    public ICommand EnableVisibleCommand { get; }
    public ICommand DisableVisibleCommand { get; }
    public ICommand SetFilterCommand { get; }

    public LibraryViewModel()
    {
        // Commands moved to ModItemViewModel for component reusability
        ToggleEnabledCommand = new RelayCommand(obj => { });
        UninstallCommand = new RelayCommand(obj =>
        {
            if (obj is not ModItemViewModel vm) return;
            if (SelectedMod?.Id == vm.Id) SelectedMod = null;
        });

        RefreshCommand = new RelayCommand(() =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
            OnPropertyChanged(nameof(DisabledMods));
        });

        EnableVisibleCommand = new RelayCommand(() =>
        {
            foreach (var vm in FilteredMods.Where(m => !m.IsEnabled))
                _library.SetEnabled(vm.Id, true);
            RefreshFiltered();
            OnPropertyChanged(nameof(EnabledMods));
            OnPropertyChanged(nameof(DisabledMods));
        }, () => FilteredMods.Any(m => !m.IsEnabled));

        DisableVisibleCommand = new RelayCommand(() =>
        {
            foreach (var vm in FilteredMods.Where(m => m.IsEnabled))
                _library.SetEnabled(vm.Id, false);
            RefreshFiltered();
            OnPropertyChanged(nameof(EnabledMods));
            OnPropertyChanged(nameof(DisabledMods));
        }, () => FilteredMods.Any(m => m.IsEnabled));

        OpenModFolderCommand = new RelayCommand(obj =>
        {
            var path = obj is ModItemViewModel vm
                ? vm.Model.InstallPath
                : _selectedMod?.Model.InstallPath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Process.Start("explorer.exe", path);
        });

        SetFilterCommand = new RelayCommand(filter =>
        {
            if (filter is not string filterStr || string.IsNullOrEmpty(filterStr)) return;
            _riskFilter = filterStr;
            RefreshFiltered();
        });

        _library.Mods.CollectionChanged += (_, _) =>
        {
            RefreshFiltered();
            OnPropertyChanged(nameof(TotalMods));
            OnPropertyChanged(nameof(EnabledMods));
            OnPropertyChanged(nameof(DisabledMods));
        };

        RefreshFiltered();
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

        if (_riskFilter != "All")
            mods = mods.Where(m =>
            {
                var itemVm = new ModItemViewModel(m);
                return itemVm.RiskTier.Equals(_riskFilter, StringComparison.OrdinalIgnoreCase);
            });

        mods = _selectedSort switch
        {
            "Installed: Oldest first" => mods.OrderBy(m => m.InstalledAt),
            "Name: A to Z" => mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "Name: Z to A" => mods.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "Author: A to Z" => mods.OrderBy(m => m.Author, StringComparer.OrdinalIgnoreCase),
            "Enabled first" => mods.OrderByDescending(m => m.IsEnabled).ThenByDescending(m => m.InstalledAt),
            _ => mods.OrderByDescending(m => m.InstalledAt),
        };

        foreach (var mod in mods)
            FilteredMods.Add(new ModItemViewModel(mod));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FilteredCount));
    }
}
