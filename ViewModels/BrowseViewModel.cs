using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.Views;

namespace LSPDFRManager.ViewModels;

public class BrowseViewModel : ObservableObject, IDisposable
{
    private readonly Action<string> _bridgeDetectingHandler;
    private readonly Action<ModInfo> _bridgeStagedHandler;
    private readonly Action<string, string> _bridgeFailedHandler;
    private bool _disposed;

    private string _currentUrl = "";
    private string _statusMessage = "Ready";
    private bool _isLoading;
    private int _loadProgress;
    private bool _isBrowserReady;

    public BrowseView? View { get; set; }

    public BrowseViewModel()
    {
        NavigateCommand = new RelayCommand(() => View?.NavigateTo(CurrentUrl));

        var bridge = ModDownloadBridge.Instance;
        _bridgeDetectingHandler = name => StatusMessage = $"Detecting mod type for {name}…";
        _bridgeStagedHandler = mod  => { StatusMessage = $"Staged: {mod.Name} — review it in the Install tab."; IsLoading = false; };
        _bridgeFailedHandler = (name, err) => { StatusMessage = $"Failed to stage {name}: {err}"; IsLoading = false; };

        bridge.Detecting += _bridgeDetectingHandler;
        bridge.Staged   += _bridgeStagedHandler;
        bridge.Failed   += _bridgeFailedHandler;
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

    public bool IsBrowserReady
    {
        get => _isBrowserReady;
        set
        {
            if (SetProperty(ref _isBrowserReady, value))
                OnPropertyChanged(nameof(CanTriggerInstall));
        }
    }

    public bool CanTriggerInstall => IsBrowserReady;

    public ICommand NavigateCommand { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        var bridge = ModDownloadBridge.Instance;
        bridge.Detecting -= _bridgeDetectingHandler;
        bridge.Staged -= _bridgeStagedHandler;
        bridge.Failed -= _bridgeFailedHandler;

        _disposed = true;
    }
}
