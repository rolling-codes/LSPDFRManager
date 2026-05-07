using System.Windows.Input;
using LSPDFRManager.Services;
using LSPDFRManager.Views;

namespace LSPDFRManager.ViewModels;

public class BrowseViewModel : ObservableObject
{
    private string _currentUrl = "";
    private string _statusMessage = "Ready";
    private bool _isLoading;
    private int _loadProgress;

    public BrowseView? View { get; set; }

    public BrowseViewModel()
    {
        NavigateCommand = new RelayCommand(() => View?.NavigateTo(CurrentUrl));

        var bridge = ModDownloadBridge.Instance;
        bridge.Detecting += name => StatusMessage = $"Detecting mod type for {name}…";
        bridge.Queued   += mod  => { StatusMessage = $"Queued: {mod.Name} — check the Install tab."; IsLoading = false; };
        bridge.Failed   += (name, err) => { StatusMessage = $"Failed to queue {name}: {err}"; IsLoading = false; };
    }

    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetProperty(ref _currentUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int LoadProgress
    {
        get => _loadProgress;
        set => SetProperty(ref _loadProgress, value);
    }

    public ICommand NavigateCommand { get; }
}
