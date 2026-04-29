using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class InstallViewModel : ObservableObject
{
    private readonly ModDetector _detector = new();
    private readonly InstallQueue _queue = InstallQueue.Instance;

    private string? _droppedPath;
    private ModInfo? _detectedMod;
    private bool _isDetecting;
    private bool _isInstalling;
    private string _authorOverride = "";

    public event Action<string>? LogAdded;

    public ObservableCollection<string> Log { get; } = [];

    public string? DroppedPath
    {
        get => _droppedPath;
        set => SetProperty(ref _droppedPath, value);
    }

    public ModInfo? DetectedMod
    {
        get => _detectedMod;
        set => SetProperty(ref _detectedMod, value);
    }

    public bool IsDetecting
    {
        get => _isDetecting;
        set { SetProperty(ref _isDetecting, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set { SetProperty(ref _isInstalling, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isDetecting && !_isInstalling;

    public string AuthorOverride
    {
        get => _authorOverride;
        set => SetProperty(ref _authorOverride, value);
    }

    private string _nameOverride = "";
    public string NameOverride
    {
        get => _nameOverride;
        set => SetProperty(ref _nameOverride, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand ClearCommand { get; }

    public InstallViewModel()
    {
        BrowseCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Mod Archive",
                Filter = "Mod Archives|*.zip;*.rar;*.7z|All Files|*.*",
            };
            if (dlg.ShowDialog() == true)
                _ = DetectAsync(dlg.FileName);
        });

        InstallCommand = new RelayCommand(() => _ = InstallAsync(), () => DetectedMod is not null && IsIdle);

        ClearCommand = new RelayCommand(() =>
        {
            DroppedPath = null;
            DetectedMod = null;
            AuthorOverride = "";
            NameOverride = "";
            Log.Clear();
        });

        _queue.InstallStarted += mod => UiDispatcher.Invoke(() => AddLog($"Installing {mod.Name}…"));

        _queue.InstallCompleted += mod => UiDispatcher.Invoke(() =>
        {
            IsInstalling = false;
            AddLog($"✓ Installed: {mod.Name}");
        });

        _queue.InstallFailed += (mod, err) => UiDispatcher.Invoke(() =>
        {
            IsInstalling = false;
            AddLog($"✗ Failed: {mod.Name} — {err}");
        });
    }

    public async Task DetectAsync(string path)
    {
        DroppedPath = path;
        DetectedMod = null;
        IsDetecting = true;

        try
        {
            var info = await Task.Run(() => _detector.Detect(path));
            UiDispatcher.Invoke(() =>
            {
                DetectedMod = info;
                NameOverride = info.Name;
                AddLog($"→ {info.TypeLabel}");
            });
        }
        finally
        {
            UiDispatcher.Invoke(() => IsDetecting = false);
        }
    }

    private async Task InstallAsync()
    {
        if (_detectedMod is null) return;

        IsInstalling = true;
        AddLog($"Queued: {_detectedMod.Name}");
        _queue.Enqueue(_detectedMod);
    }

    private void AddLog(string msg)
    {
        UiDispatcher.Invoke(() =>
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            LogAdded?.Invoke(msg);
        });
    }
}
