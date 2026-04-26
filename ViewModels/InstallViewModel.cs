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
        set
        {
            SetProperty(ref _detectedMod, value);
            OnPropertyChanged(nameof(HasDetection));
            OnPropertyChanged(nameof(HasNoDetection));
            OnPropertyChanged(nameof(DetectedFilesSample));
            OnPropertyChanged(nameof(HasMoreDetectedFiles));
            OnPropertyChanged(nameof(DetectedFilesMoreText));
        }
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

    public bool IsIdle         => !_isDetecting && !_isInstalling;
    public bool HasDetection   => _detectedMod is not null;
    public bool HasNoDetection => _detectedMod is null;

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

    // ── File preview ──────────────────────────────────────────────
    private const int FilePreviewCount = 8;

    public IEnumerable<string> DetectedFilesSample =>
        _detectedMod?.Files
            .Take(FilePreviewCount)
            .Select(f => Path.GetFileName(f) is { Length: > 0 } n ? n : f)
            ?? [];

    public bool HasMoreDetectedFiles =>
        (_detectedMod?.Files.Count ?? 0) > FilePreviewCount;

    public string DetectedFilesMoreText =>
        HasMoreDetectedFiles
            ? $"+{_detectedMod!.Files.Count - FilePreviewCount} more files"
            : "";

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

        InstallCommand = new RelayCommand(
            () => _ = InstallAsync(),
            () => HasDetection && IsIdle);

        ClearCommand = new RelayCommand(() =>
        {
            DroppedPath    = null;
            DetectedMod    = null;
            AuthorOverride = "";
            NameOverride   = "";
            Log.Clear();
        });

        _queue.InstallStarted   += mod => AddLog($"Installing {mod.Name}…");
        _queue.InstallCompleted += mod =>
        {
            IsInstalling = false;
            AddLog($"✓ Installed: {mod.Name}");
            if (AppConfig.Instance.AutoLaunchAfterInstall)
            {
                AddLog("Auto-launching LSPDFR...");
                var hook = Path.Combine(AppConfig.Instance.GtaPath, "RAGEPluginHook.exe");
                if (File.Exists(hook))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(hook)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = AppConfig.Instance.GtaPath,
                    });
                }
            }
        };
        _queue.InstallFailed    += (mod, err) => { IsInstalling = false; AddLog($"✗ Failed: {mod.Name} — {err}"); };
    }

    public async Task DetectAsync(string path)
    {
        DroppedPath = path;
        DetectedMod = null;
        IsDetecting = true;
        AddLog($"Detecting: {System.IO.Path.GetFileName(path)}");

        try
        {
            var info = await Task.Run(() => _detector.Detect(path));
            DetectedMod  = info;
            NameOverride = info.Name;
            AddLog($"→ {info.TypeLabel}  ({info.ConfidenceLabel} confidence)");
            foreach (var w in info.Warnings) AddLog($"⚠ {w}");
        }
        catch (Exception ex)
        {
            AddLog($"Detection error: {ex.Message}");
        }
        finally
        {
            IsDetecting = false;
        }
    }

    private async Task InstallAsync()
    {
        if (_detectedMod is null) return;

        var gtaPath = AppConfig.Instance.GtaPath;
        if (!System.IO.Directory.Exists(gtaPath))
        {
            AddLog($"✗ GTA V path not found: {gtaPath}");
            AddLog("Please set your GTA V path in Settings.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_nameOverride))
            _detectedMod.Name = _nameOverride;
        if (!string.IsNullOrWhiteSpace(_authorOverride))
            _detectedMod.Author = _authorOverride;

        IsInstalling = true;
        AddLog($"Queued: {_detectedMod.Name}");
        _queue.Enqueue(_detectedMod);
    }

    private void AddLog(string msg)
    {
        Log.Add($"[{DateTime.Now:HH:mm:ss}]  {msg}");
        LogAdded?.Invoke(msg);
    }
}
