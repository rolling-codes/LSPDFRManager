using System.Diagnostics;
using System.Windows.Input;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class MainViewModel : ObservableObject
{
    private object _currentView;
    private string _activePage = "Library";
    private string _statusMessage = "Ready";

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

    public bool IsLibraryActive  => _activePage == "Library";
    public bool IsInstallActive  => _activePage == "Install";
    public bool IsConfigActive   => _activePage == "Config";
    public bool IsBrowseActive   => _activePage == "Browse";
    public bool IsSettingsActive => _activePage == "Settings";

    public ICommand NavigateCommand    { get; }
    public ICommand LaunchLspdfrCommand { get; }

    public MainViewModel()
    {
        _currentView = LibraryVM;

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

        LaunchLspdfrCommand = new RelayCommand(
            () =>
            {
                var hook = Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe");
                Process.Start(new ProcessStartInfo(hook)
                {
                    UseShellExecute  = true,
                    WorkingDirectory = AppConfig.Instance.GtaPath,
                });
            },
            () => LspdfrStatusService.Instance.IsLspdfrInstalled);

        InstallVM.LogAdded += msg => StatusMessage = msg;
    }
}
