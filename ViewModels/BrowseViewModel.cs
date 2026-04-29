using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class BrowseViewModel : ObservableObject
{
    private readonly LspdfrApiClient _api = LspdfrApiClient.Instance;
    private readonly ModDetector _detector = new();

    private string _searchQuery = "";
    private string _statusMessage = "";
    private bool _isSearching;
    private bool _isDownloading;
    private ModSearchResult? _selectedResult;

    public ObservableCollection<ModSearchResult> Results { get; } = [];

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
        set => SetProperty(ref _isSearching, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    public ModSearchResult? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand InstallCommand { get; }

    public BrowseViewModel()
    {
        SearchCommand = new RelayCommand(() => _ = SearchAsync());
        InstallCommand = new RelayCommand(obj => _ = InstallAsync(obj as ModSearchResult));
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery)) return;

        IsSearching = true;
        Results.Clear();

        try
        {
            var results = await _api.SearchAsync(_searchQuery, null);
            UiDispatcher.Invoke(() =>
            {
                foreach (var r in results) Results.Add(r);
            });
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task InstallAsync(ModSearchResult? result)
    {
        if (result is null) return;

        var info = await Task.Run(() => _detector.Detect(result.Title));
        var queue = InstallQueue.Instance;
        queue.Enqueue(info);

        UiDispatcher.Invoke(() => StatusMessage = "Queued install");
    }
}
