using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class BrowseViewModel : ObservableObject
{
    private readonly LspdfrApiClient _api = LspdfrApiClient.Instance;
    private readonly ModDetector _detector = new();
    private readonly InstallQueue _queue = InstallQueue.Instance;

    private string _searchQuery = "";
    private string _selectedCategory = "All";
    private string _statusMessage = "Ready";
    private bool _isSearching;
    private bool _isDownloading;
    private bool _apiAvailable;
    private int _downloadProgress;
    private ModSearchResult? _selectedResult;

    public BrowseViewModel()
    {
        SearchCommand = new RelayCommand(() => _ = SearchAsync(), () => IsIdle && !string.IsNullOrWhiteSpace(SearchQuery));
        InstallCommand = new RelayCommand(item => _ = InstallAsync(item as ModSearchResult ?? SelectedResult), _ => IsIdle);
        OpenPageCommand = new RelayCommand(item => OpenPage(item as ModSearchResult ?? SelectedResult), _ => SelectedResult is not null);

        _ = RefreshAvailabilityAsync();
    }

    public ObservableCollection<ModSearchResult> Results { get; } = [];

    public List<string> Categories { get; } =
    [
        "All",
        "Plugins",
        "Vehicles",
        "Scripts",
        "Maps",
        "EUP",
        "Misc",
    ];

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (SetProperty(ref _isSearching, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsSearching && !IsDownloading;

    public bool ApiAvailable
    {
        get => _apiAvailable;
        set => SetProperty(ref _apiAvailable, value);
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public ModSearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (!SetProperty(ref _selectedResult, value))
                return;

            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedResult is not null;

    public ICommand SearchCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand OpenPageCommand { get; }

    private async Task RefreshAvailabilityAsync()
    {
        ApiAvailable = await _api.IsAvailableAsync();
        StatusMessage = ApiAvailable
            ? "Search the LSPDFR catalog."
            : "Browse API unavailable. Start the local API service first.";
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsSearching = true;
        StatusMessage = "Searching…";
        Results.Clear();

        try
        {
            var results = await _api.SearchAsync(SearchQuery.Trim(), SelectedCategory);
            foreach (var result in results)
                Results.Add(result);

            StatusMessage = results.Count == 0
                ? "No results found."
                : $"Found {results.Count} result(s).";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task InstallAsync(ModSearchResult? result)
    {
        if (result is null)
            return;

        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
        {
            StatusMessage = "This result does not have a downloadable file.";
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = $"Downloading {result.Title}…";

        try
        {
            var progress = new Progress<int>(value => DownloadProgress = value);
            var fileName = BuildArchiveName(result);
            var downloadedPath = await _api.DownloadToTempAsync(result.DownloadUrl, fileName, progress);

            if (string.IsNullOrWhiteSpace(downloadedPath))
            {
                StatusMessage = "Download failed.";
                return;
            }

            var mod = await Task.Run(() => _detector.Detect(downloadedPath));
            mod.Name = string.IsNullOrWhiteSpace(result.Title) ? mod.Name : result.Title;
            mod.Author = result.Author;

            _queue.Enqueue(mod);
            StatusMessage = $"Queued install: {mod.Name}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private static string BuildArchiveName(ModSearchResult result)
    {
        var stem = string.IsNullOrWhiteSpace(result.Title)
            ? $"mod_{DateTime.Now:yyyyMMddHHmmss}"
            : string.Concat(result.Title.Split(Path.GetInvalidFileNameChars()));

        return stem.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? stem
            : $"{stem}.zip";
    }

    private static void OpenPage(ModSearchResult? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Url))
            return;

        Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
    }
}
