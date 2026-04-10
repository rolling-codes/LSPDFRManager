using System.Windows.Input;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// Powers the Browse tab — searches lcpdfr.com via the local API backend
/// and lets users download and install mods directly from the app.
/// </summary>
public class BrowseViewModel : ObservableObject
{
    private readonly LspdfrApiClient _api = LspdfrApiClient.Instance;
    private readonly ModDetector     _detector = new();

    private string  _searchQuery  = "";
    private string  _statusMessage = "";
    private bool    _isSearching;
    private bool    _isDownloading;
    private string  _selectedCategory = "All";
    private ModSearchResult? _selectedResult;
    private int     _downloadProgress;
    private bool    _apiAvailable;

    public ObservableCollection<ModSearchResult> Results  { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set { SetProperty(ref _isSearching, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set { SetProperty(ref _isDownloading, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle    => !_isSearching && !_isDownloading;
    public bool ApiAvailable
    {
        get => _apiAvailable;
        private set => SetProperty(ref _apiAvailable, value);
    }

    public ModSearchResult? SelectedResult
    {
        get => _selectedResult;
        set { SetProperty(ref _selectedResult, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedResult is not null;

    public int DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { SetProperty(ref _selectedCategory, value); }
    }

    public List<string> Categories { get; } =
    [
        "All", "LSPDFR Plugins", "Vehicles", "Scripts", "EUP", "Maps", "Sounds"
    ];

    public ICommand SearchCommand   { get; }
    public ICommand InstallCommand  { get; }
    public ICommand OpenPageCommand { get; }

    public BrowseViewModel()
    {
        SearchCommand = new RelayCommand(
            () => _ = SearchAsync(),
            () => IsIdle && !string.IsNullOrWhiteSpace(_searchQuery));

        InstallCommand = new RelayCommand(
            obj => _ = DownloadAndInstallAsync(obj as ModSearchResult ?? _selectedResult),
            obj => IsIdle && (obj as ModSearchResult ?? _selectedResult) is not null);

        OpenPageCommand = new RelayCommand(
            obj =>
            {
                var url = (obj as ModSearchResult ?? _selectedResult)?.Url;
                if (!string.IsNullOrEmpty(url))
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(url)
                        { UseShellExecute = true });
            },
            obj => (obj as ModSearchResult ?? _selectedResult) is not null);

        // Check API availability in the background when this VM is first created
        _ = CheckApiAsync();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task CheckApiAsync()
    {
        ApiAvailable = await _api.IsAvailableAsync().ConfigureAwait(false);
        StatusMessage = ApiAvailable
            ? "Connected to LSPDFR Manager API."
            : "API server not running — start LSPDFRManager.Api.exe to enable Browse.";
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery)) return;

        IsSearching = true;
        Results.Clear();
        SelectedResult = null;
        StatusMessage  = $"Searching for \"{_searchQuery}\"…";

        try
        {
            var category = _selectedCategory == "All" ? null : _selectedCategory;
            var results  = await _api.SearchAsync(_searchQuery, category)
                                     .ConfigureAwait(false);

            foreach (var r in results) Results.Add(r);

            StatusMessage = results.Count > 0
                ? $"{results.Count} result(s) found."
                : "No results found. Try a different search term.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task DownloadAndInstallAsync(ModSearchResult? result)
    {
        if (result is null || string.IsNullOrEmpty(result.DownloadUrl)) return;

        IsDownloading   = true;
        DownloadProgress = 0;
        StatusMessage   = $"Downloading {result.Title}…";

        try
        {
            var ext      = Path.GetExtension(result.DownloadUrl);
            var fileName = $"{SanitizeFileName(result.Title)}{(string.IsNullOrEmpty(ext) ? ".zip" : ext)}";
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                StatusMessage    = $"Downloading {result.Title}… {p}%";
            });

            var tempPath = await _api.DownloadToTempAsync(result.DownloadUrl, fileName, progress)
                                     .ConfigureAwait(false);
            if (tempPath is null)
            {
                StatusMessage = $"Download failed for {result.Title}.";
                return;
            }

            StatusMessage = $"Detecting mod type…";
            var info      = await Task.Run(() => _detector.Detect(tempPath)).ConfigureAwait(false);

            // Apply metadata from API result
            if (string.IsNullOrEmpty(info.Author)) info.Author = result.Author;
            if (string.IsNullOrEmpty(info.Version)) info.Version = result.Version;

            var queue = new Core.InstallQueue();
            queue.InstallCompleted += mod =>
                System.Windows.Application.Current.Dispatcher.Invoke(
                    () => StatusMessage = $"✓ Installed: {mod.Name}");
            queue.InstallFailed += (_, err) =>
                System.Windows.Application.Current.Dispatcher.Invoke(
                    () => StatusMessage = $"✗ Install failed: {err}");

            queue.Enqueue(info);
            StatusMessage = $"Queued for install: {info.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading    = false;
            DownloadProgress = 0;
        }
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
