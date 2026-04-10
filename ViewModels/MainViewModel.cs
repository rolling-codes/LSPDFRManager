using System.Windows.Input;

namespace LSPDFRManager.ViewModels;

public class MainViewModel : ObservableObject
{
    private object _currentView;
    private string _activePage = "Library";
    private string _statusMessage = "Ready";

    public LibraryViewModel LibraryVM { get; } = new();
    public InstallViewModel InstallVM { get; } = new();
    public KeysViewModel KeysVM { get; } = new();
    public SettingsViewModel SettingsVM { get; } = new();

    public object CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLibraryActive  => _activePage == "Library";
    public bool IsInstallActive  => _activePage == "Install";
    public bool IsKeysActive     => _activePage == "Keys";
    public bool IsSettingsActive => _activePage == "Settings";

    public ICommand NavigateCommand { get; }

    public MainViewModel()
    {
        _currentView = LibraryVM;

        NavigateCommand = new RelayCommand(page =>
        {
            _activePage = page?.ToString() ?? "Library";

            CurrentView = _activePage switch
            {
                "Install"  => InstallVM,
                "Keys"     => KeysVM,
                "Settings" => SettingsVM,
                _          => LibraryVM,
            };

            OnPropertyChanged(nameof(IsLibraryActive));
            OnPropertyChanged(nameof(IsInstallActive));
            OnPropertyChanged(nameof(IsKeysActive));
            OnPropertyChanged(nameof(IsSettingsActive));
        });

        // Propagate install log messages to status bar
        InstallVM.LogAdded += msg => StatusMessage = msg;
    }
}
