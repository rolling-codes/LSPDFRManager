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

    public MainViewModel()
    {
        _currentView = LibraryVM;

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
    }

    public LibraryViewModel LibraryVM { get; } = new();
    public InstallViewModel InstallVM { get; } = new();
    public ConfigViewModel ConfigVM { get; } = new();
    public BrowseViewModel BrowseVM { get; } = new();
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

    public bool IsLibraryActive => _activePage == "Library";
    public bool IsInstallActive => _activePage == "Install";
    public bool IsConfigActive => _activePage == "Config";
    public bool IsBrowseActive => _activePage == "Browse";
    public bool IsSettingsActive => _activePage == "Settings";

    public ICommand NavigateCommand { get; }
    public ICommand LaunchLspdfrCommand { get; }

    private void Navigate(object? page)
    {
        _activePage = page?.ToString() ?? "Library";

        CurrentView = _activePage switch
        {
            "Install" => InstallVM,
            "Config" => ConfigVM,
            "Browse" => BrowseVM,
            "Settings" => SettingsVM,
            _ => LibraryVM,
        };

        OnPropertyChanged(nameof(IsLibraryActive));
        OnPropertyChanged(nameof(IsInstallActive));
        OnPropertyChanged(nameof(IsConfigActive));
        OnPropertyChanged(nameof(IsBrowseActive));
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
