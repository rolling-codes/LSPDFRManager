using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class LibraryViewModel : ObservableObject
{
    private const int FileSampleSize = 10;

    private readonly ModLibraryService _library = ModLibraryService.Instance;

    private string _searchQuery = "";
    private string _selectedFilter = "All";
    private string _selectedSort = "Installed: Newest first";
    private string _riskFilter = "All";
    private ModItemViewModel? _selectedMod;
    private List<(Guid Id, bool WasEnabled)>? _lastBulkToggle;

    public LibraryViewModel()
    {
        ToggleEnabledCommand = new RelayCommand(static _ => { });
        UninstallCommand = new RelayCommand(static _ => { });

        RefreshCommand = new RelayCommand(Refresh);
        EnableVisibleCommand = new RelayCommand(
            () => SetVisibleModsEnabled(true),
            () => FilteredMods.Any(mod => !mod.IsEnabled));

        DisableVisibleCommand = new RelayCommand(
            () => SetVisibleModsEnabled(false),
            () => FilteredMods.Any(mod => mod.IsEnabled));

        OpenModFolderCommand = new RelayCommand(OpenModFolder);
        MoveSelectedUpCommand = new RelayCommand(() => MoveSelected(-1), () => SelectedMod is not null);
        MoveSelectedDownCommand = new RelayCommand(() => MoveSelected(1), () => SelectedMod is not null);
        UndoBulkToggleCommand = new RelayCommand(UndoBulkToggle, () => _lastBulkToggle?.Count > 0);
        ExportEnabledModsCommand = new RelayCommand(ExportEnabledMods, () => _library.Mods.Any(mod => mod.IsEnabled));
        SetFilterCommand = new RelayCommand(filter =>
        {
            if (filter is string risk)
                RiskFilter = risk;
        });

        _library.Mods.CollectionChanged += OnLibraryChanged;
        RefreshFiltered();
    }

    public ObservableCollection<ModItemViewModel> FilteredMods { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RecordSearchHistory(value);
                RefreshFiltered();
            }
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
                RefreshFiltered();
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
                RefreshFiltered();
        }
    }

    public string RiskFilter
    {
        get => _riskFilter;
        set
        {
            if (SetProperty(ref _riskFilter, value))
            {
                RefreshFiltered();
                OnPropertyChanged(nameof(IsRiskAllActive));
                OnPropertyChanged(nameof(IsRiskSafeActive));
                OnPropertyChanged(nameof(IsRiskMediumActive));
                OnPropertyChanged(nameof(IsRiskHighActive));
            }
        }
    }

    public ModItemViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!SetProperty(ref _selectedMod, value))
                return;

            OnPropertyChanged(nameof(HasSelectedMod));
            OnPropertyChanged(nameof(HasNoSelectedMod));
            OnPropertyChanged(nameof(SelectedModFilesSample));
            OnPropertyChanged(nameof(HasMoreSelectedFiles));
            OnPropertyChanged(nameof(SelectedModFilesMoreText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int TotalMods => _library.Mods.Count;
    public int EnabledMods => _library.Mods.Count(mod => mod.IsEnabled);
    public int DisabledMods => _library.Mods.Count(mod => !mod.IsEnabled);
    public int FilteredCount => FilteredMods.Count;
    public bool IsEmpty => FilteredMods.Count == 0;
    public IReadOnlyList<string> SearchHistory => AppConfig.Instance.LibrarySearchHistory;

    public bool HasSelectedMod => SelectedMod is not null;
    public bool HasNoSelectedMod => SelectedMod is null;
    public bool IsRiskAllActive => RiskFilter.Equals("All", StringComparison.OrdinalIgnoreCase);
    public bool IsRiskSafeActive => RiskFilter.Equals("Safe", StringComparison.OrdinalIgnoreCase);
    public bool IsRiskMediumActive => RiskFilter.Equals("Medium", StringComparison.OrdinalIgnoreCase);
    public bool IsRiskHighActive => RiskFilter.Equals("High", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<string> SelectedModFilesSample =>
        SelectedMod?.InstalledFiles
            .Select(file => Path.GetFileName(file) is { Length: > 0 } name ? name : file)
            .Take(FileSampleSize)
        ?? [];

    public bool HasMoreSelectedFiles =>
        (SelectedMod?.InstalledFiles.Count ?? 0) > FileSampleSize;

    public string SelectedModFilesMoreText =>
        HasMoreSelectedFiles
            ? $"+{SelectedMod!.InstalledFiles.Count - FileSampleSize} more files"
            : "";

    public List<string> FilterOptions { get; } =
    [
        "All",
        "LSPDFR Plugin",
        "Vehicle Add-On DLC",
        "Vehicle Replace",
        "ASI Mod",
        "Script (CS/VB)",
        "EUP Clothing",
        "Map / MLO",
        "Sound Pack",
        "Miscellaneous",
    ];

    public List<string> SortOptions { get; } =
    [
        "Installed: Newest first",
        "Installed: Oldest first",
        "Name: A to Z",
        "Name: Z to A",
        "Author: A to Z",
        "Enabled first",
        "Load order",
    ];

    public ICommand ToggleEnabledCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenModFolderCommand { get; }
    public ICommand EnableVisibleCommand { get; }
    public ICommand DisableVisibleCommand { get; }
    public ICommand UndoBulkToggleCommand { get; }
    public ICommand MoveSelectedUpCommand { get; }
    public ICommand MoveSelectedDownCommand { get; }
    public ICommand ExportEnabledModsCommand { get; }
    public ICommand SetFilterCommand { get; }

    private void Refresh()
    {
        RefreshFiltered();
        RaiseCountsChanged();
    }

    private void SetVisibleModsEnabled(bool enabled)
    {
        var targets = FilteredMods.Where(mod => mod.IsEnabled != enabled).ToList();
        _lastBulkToggle = targets.Select(mod => (mod.Id, mod.IsEnabled)).ToList();
        var ids = targets.Select(mod => mod.Id);
        _library.SetEnabledBatch(ids, enabled);
        Refresh();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UndoBulkToggle()
    {
        if (_lastBulkToggle is null || _lastBulkToggle.Count == 0)
            return;

        foreach (var (id, wasEnabled) in _lastBulkToggle)
            _library.SetEnabled(id, wasEnabled);

        _lastBulkToggle = null;
        Refresh();
        CommandManager.InvalidateRequerySuggested();
    }

    private void MoveSelected(int direction)
    {
        if (SelectedMod is null)
            return;

        var selectedId = SelectedMod.Id;
        _library.Reorder(selectedId, direction);
        RefreshFiltered();
        SelectedMod = FilteredMods.FirstOrDefault(mod => mod.Id == selectedId);
    }

    private void ExportEnabledMods()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Enabled Mods",
            Filter = "Markdown|*.md|Text|*.txt",
            FileName = $"enabled-mods-{DateTime.Now:yyyy-MM-dd}.md",
        };

        if (dialog.ShowDialog() != true)
            return;

        var lines = _library.Mods
            .Where(mod => mod.IsEnabled)
            .OrderBy(mod => mod.LoadOrderPriority == 0 ? int.MaxValue : mod.LoadOrderPriority)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .Select(mod => $"- {mod.Name} ({mod.TypeLabel}){(string.IsNullOrWhiteSpace(mod.Version) ? "" : $" v{mod.Version}")}");

        File.WriteAllLines(dialog.FileName, ["# Enabled LSPDFR Mods", "", .. lines]);
    }

    private void OpenModFolder(object? parameter)
    {
        var path = parameter is ModItemViewModel vm
            ? vm.Model.InstallPath
            : SelectedMod?.Model.InstallPath;

        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            Process.Start("explorer.exe", path);
    }

    private void OnLibraryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFiltered();
        RaiseCountsChanged();

        if (SelectedMod is not null &&
            !_library.Mods.Any(mod => mod.Id == SelectedMod.Id))
        {
            SelectedMod = null;
        }
    }

    private void RefreshFiltered()
    {
        var filtered = ApplyFilters(_library.Mods)
            .Select(mod => new ModItemViewModel(mod))
            .ToList();

        FilteredMods.Clear();
        foreach (var item in filtered)
            FilteredMods.Add(item);

        if (SelectedMod is not null)
            SelectedMod = FilteredMods.FirstOrDefault(mod => mod.Id == SelectedMod.Id);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FilteredCount));
    }

    private IEnumerable<InstalledMod> ApplyFilters(IEnumerable<InstalledMod> mods)
    {
        var query = SearchQuery.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            mods = _library.Search(query);

        if (!SelectedFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            mods = mods.Where(mod =>
                mod.TypeLabel.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!RiskFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            mods = mods.Where(mod =>
                RiskFor(mod.DetectionScore).Equals(RiskFilter, StringComparison.OrdinalIgnoreCase));
        }

        return SelectedSort switch
        {
            "Installed: Oldest first" => mods.OrderBy(mod => mod.InstalledAt),
            "Name: A to Z" => mods.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase),
            "Name: Z to A" => mods.OrderByDescending(mod => mod.Name, StringComparer.OrdinalIgnoreCase),
            "Author: A to Z" => mods.OrderBy(mod => mod.Author, StringComparer.OrdinalIgnoreCase),
            "Enabled first" => mods.OrderByDescending(mod => mod.IsEnabled).ThenByDescending(mod => mod.InstalledAt),
            "Load order" => mods.OrderBy(mod => mod.LoadOrderPriority == 0 ? int.MaxValue : mod.LoadOrderPriority).ThenBy(mod => mod.InstalledAt),
            _ => mods.OrderByDescending(mod => mod.InstalledAt),
        };
    }

    private void RecordSearchHistory(string value)
    {
        var query = value.Trim();
        if (query.Length < 2)
            return;

        var history = AppConfig.Instance.LibrarySearchHistory;
        history.RemoveAll(item => item.Equals(query, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, query);
        if (history.Count > 10)
            history.RemoveRange(10, history.Count - 10);

        AppConfig.Instance.Save();
        OnPropertyChanged(nameof(SearchHistory));
    }

    private void RaiseCountsChanged()
    {
        OnPropertyChanged(nameof(TotalMods));
        OnPropertyChanged(nameof(EnabledMods));
        OnPropertyChanged(nameof(DisabledMods));
    }

    private static string RiskFor(int score) =>
        score >= 70 ? "Safe" :
        score >= 40 ? "Medium" :
        "High";
}
