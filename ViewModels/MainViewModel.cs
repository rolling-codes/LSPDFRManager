using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class MainViewModel : ObservableObject
{
    private object _currentView;
    private string _activePage = "Library";
    private string _statusMessage = "Ready";
    private string? _globalErrorMessage;
    private InstallQueue? _installQueue;

    public LibraryViewModel  LibraryVM  { get; } = new();
    public InstallViewModel  InstallVM  { get; } = new();
    public ConfigViewModel   ConfigVM   { get; } = new();
    public BrowseViewModel   BrowseVM   { get; } = new();
    public SettingsViewModel SettingsVM { get; } = new();

    /// <summary>Live LSPDFR / GTA V status — bound in the sidebar.</summary>
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
        set => SetProperty(ref _globalErrorMessage, value);
    }

    public bool HasGlobalError => !string.IsNullOrEmpty(GlobalErrorMessage);

    public bool IsLibraryActive  => _activePage == "Library";
    public bool IsInstallActive  => _activePage == "Install";
    public bool IsConfigActive   => _activePage == "Config";
    public bool IsBrowseActive   => _activePage == "Browse";
    public bool IsSettingsActive => _activePage == "Settings";

    public ICommand NavigateCommand    { get; }
    public ICommand LaunchLspdfrCommand { get; }

    public MainViewModel()
    {
        AppLogger.Info("[MAINVIEWMODEL] Constructor starting");
        try
        {
            AppLogger.Info("[MAINVIEWMODEL] Initializing LibraryVM");
            _currentView = LibraryVM;

            AppLogger.Info("[MAINVIEWMODEL] Setting up NavigateCommand");
            NavigateCommand = new RelayCommand(page =>
            {
                _activePage = page?.ToString() ?? "Library";

                CurrentView = _activePage switch
                {
                    "Install"  => InstallVM,
                    "Config"   => ConfigVM,
                    "Browse"   => BrowseVM,
                    "Settings" => SettingsVM,
                    _          => LibraryVM,
                };

                OnPropertyChanged(nameof(IsLibraryActive));
                OnPropertyChanged(nameof(IsInstallActive));
                OnPropertyChanged(nameof(IsConfigActive));
                OnPropertyChanged(nameof(IsBrowseActive));
                OnPropertyChanged(nameof(IsSettingsActive));
            });

            AppLogger.Info("[MAINVIEWMODEL] Setting up LaunchLspdfrCommand");
            LaunchLspdfrCommand = new RelayCommand(
                () =>
                {
                    AppLogger.Info("[MAINVIEWMODEL] Launching LSPDFR");
                    var hook = Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe");
                    Process.Start(new ProcessStartInfo(hook)
                    {
                        UseShellExecute  = true,
                        WorkingDirectory = AppConfig.Instance.GtaPath,
                    });
                },
                () => LspdfrStatusService.Instance.IsLspdfrInstalled);

            AppLogger.Info("[MAINVIEWMODEL] Wiring InstallVM events");
            InstallVM.LogAdded += msg => StatusMessage = msg;

            AppLogger.Info("[MAINVIEWMODEL] Initializing InstallQueue");
            _installQueue = InstallQueue.Instance;
            _installQueue.InstallFailedWithResult += (mod, result) =>
            {
                AppLogger.Error("[MAINVIEWMODEL] Install failed", null);
                GlobalErrorMessage = $"Install failed: {result.Error}";
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    GlobalErrorMessage = null;
                    OnPropertyChanged(nameof(GlobalErrorMessage));
                    OnPropertyChanged(nameof(HasGlobalError));
                });
            };

            AppLogger.Info("[MAINVIEWMODEL] Constructor completed successfully");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINVIEWMODEL] Constructor failed", ex);
            throw;
        }
    }
}
