using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class MainViewModel : ObservableObject
{
    private object _currentView;
    private string _activePage = "Home";
    private string _statusMessage = "Ready";
    private string? _globalErrorMessage;

    public MainViewModel()
    {
        _currentView = DashboardVM;

        NavigateCommand = new RelayCommand(Navigate);
        LaunchLspdfrCommand = new RelayCommand(LaunchLspdfr, () => Status.IsLspdfrInstalled);

        InstallVM.LogAdded += message => UiDispatcher.Invoke(() => StatusMessage = message);

        InstallQueue.Instance.InstallFailedWithResult += (mod, result) =>
        {
            GlobalErrorMessage = $"Install failed: {result.Error}";

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                UiDispatcher.Invoke(() => GlobalErrorMessage = null);
            });
        };

        // Load persistent services
        ChangeHistoryService.Instance.Load();
        RestorePointService.Instance.Load();
    }

    public DashboardViewModel DashboardVM { get; } = new();
    public LibraryViewModel LibraryVM { get; } = new();
    public InstallViewModel InstallVM { get; } = new();
    public ConfigViewModel ConfigVM { get; } = new();
    public BrowseViewModel BrowseVM { get; } = new();
    public DiagnosticsViewModel DiagnosticsVM { get; } = new();
    public ProfilesViewModel ProfilesVM { get; } = new();
    public BackupsViewModel BackupsVM { get; } = new();
    public HistoryViewModel HistoryVM { get; } = new();
    public LogViewerViewModel LogViewerVM { get; } = new();
    public SettingsViewModel SettingsVM { get; } = new();

    public LspdfrStatusService Status { get; } = LspdfrStatusService.Instance;

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

    public string? GlobalErrorMessage
    {
        get => _globalErrorMessage;
        set
        {
            if (SetProperty(ref _globalErrorMessage, value))
                OnPropertyChanged(nameof(HasGlobalError));
        }
    }

    public bool HasGlobalError => !string.IsNullOrWhiteSpace(GlobalErrorMessage);

    public bool IsHomeActive        => _activePage == "Home";
    public bool IsLibraryActive     => _activePage == "Library";
    public bool IsInstallActive     => _activePage == "Install";
    public bool IsBrowseActive      => _activePage == "Browse";
    public bool IsDiagnosticsActive => _activePage == "Diagnostics";
    public bool IsProfilesActive    => _activePage == "Profiles";
    public bool IsBackupsActive     => _activePage == "Backups";
    public bool IsHistoryActive     => _activePage == "History";
    public bool IsLogViewerActive   => _activePage == "Logs";
    public bool IsSettingsActive    => _activePage == "Settings";

    public ICommand NavigateCommand { get; }
    public ICommand LaunchLspdfrCommand { get; }

    private void Navigate(object? page)
    {
        _activePage = page?.ToString() ?? "Home";

        CurrentView = _activePage switch
        {
            "Library"     => LibraryVM,
            "Install"     => InstallVM,
            "Config"      => ConfigVM,
            "Browse"      => BrowseVM,
            "Diagnostics" => DiagnosticsVM,
            "Profiles"    => ProfilesVM,
            "Backups"     => BackupsVM,
            "History"     => HistoryVM,
            "Logs"        => LogViewerVM,
            "Settings"    => SettingsVM,
            _             => DashboardVM,
        };

        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsLibraryActive));
        OnPropertyChanged(nameof(IsInstallActive));
        OnPropertyChanged(nameof(IsBrowseActive));
        OnPropertyChanged(nameof(IsDiagnosticsActive));
        OnPropertyChanged(nameof(IsProfilesActive));
        OnPropertyChanged(nameof(IsBackupsActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsLogViewerActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    private void LaunchLspdfr()
    {
        var hook = Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe");
        if (!File.Exists(hook))
            return;

        Process.Start(new ProcessStartInfo(hook)
        {
            UseShellExecute = true,
            WorkingDirectory = AppConfig.Instance.GtaPath,
        });
    }
}
