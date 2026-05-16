using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class CompatibilityViewModel : ObservableObject
{
    private VersionBundle? _bundle;
    private bool _isRefreshing;

    public CompatibilityViewModel()
    {
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
    }

    public ICommand RefreshCommand { get; }

    public VersionBundle? Bundle
    {
        get => _bundle;
        private set
        {
            if (SetProperty(ref _bundle, value))
            {
                OnPropertyChanged(nameof(GtaRow));
                OnPropertyChanged(nameof(LspdfrRow));
                OnPropertyChanged(nameof(RphRow));
                OnPropertyChanged(nameof(ShvRow));
                OnPropertyChanged(nameof(ShvdnRow));
                OnPropertyChanged(nameof(HasBundle));
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public bool HasBundle => Bundle is not null;

    public ComponentRow GtaRow    => BuildRow("GTA5.exe",            Bundle?.GtaVersion,               Bundle?.GtaHash,               Bundle?.GtaPresent ?? false,            required: true);
    public ComponentRow LspdfrRow => BuildRow("LSPDFR.dll",          Bundle?.LspdfrVersion,            Bundle?.LspdfrHash,            Bundle?.LspdfrPresent ?? false,         required: false);
    public ComponentRow RphRow    => BuildRow("RAGEPluginHook.exe",  Bundle?.RagePluginHookVersion,    Bundle?.RagePluginHookHash,    Bundle?.RagePluginHookPresent ?? false, required: false);
    public ComponentRow ShvRow    => BuildRow("ScriptHookV.dll",     Bundle?.ScriptHookVVersion,       Bundle?.ScriptHookVHash,       present: Bundle?.ScriptHookVVersion is not null, required: false);
    public ComponentRow ShvdnRow  => BuildRow("ScriptHookVDotNet",   Bundle?.ScriptHookVDotNetVersion, Bundle?.ScriptHookVDotNetHash, present: Bundle?.ScriptHookVDotNetVersion is not null, required: false);

    public async Task RefreshAsync()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrWhiteSpace(gtaPath))
            return;

        IsRefreshing = true;
        try
        {
            Bundle = await new VersionDetectorService().DetectAsync(gtaPath);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private static ComponentRow BuildRow(string name, string? version, string? hash, bool present, bool required) =>
        new(name, version ?? (present ? "—" : "Not found"), hash, present, required);
}

public record ComponentRow(string Name, string Version, string? Hash, bool Present, bool Required)
{
    public string StatusLabel => Present ? "OK" : (Required ? "Missing" : "Not installed");
}
