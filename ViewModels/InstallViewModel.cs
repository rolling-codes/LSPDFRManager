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
    private string _nameOverride = "";

    public InstallViewModel()
    {
        BrowseCommand = new RelayCommand(BrowseForArchive);
        InstallCommand = new RelayCommand(() => _ = InstallAsync(), () => DetectedMod is not null && IsIdle);
        ClearCommand = new RelayCommand(Clear);

        _queue.InstallStarted += mod => AddLog($"Installing {mod.Name}…");
        _queue.InstallCompleted += mod =>
        {
            IsInstalling = false;
            AddLog($"✓ Installed: {mod.Name}");
        };
        _queue.InstallFailed += (mod, error) =>
        {
            IsInstalling = false;
            AddLog($"✗ Failed: {mod.Name} — {error}");
        };

        var bridge = ModDownloadBridge.Instance;
        bridge.Detecting += name => AddLog($"[Browse] Detecting: {name}…");
        bridge.Queued    += mod  => AddLog($"[Browse] Queued: {mod.Name} ({mod.TypeLabel})");
        bridge.Failed    += (name, err) => AddLog($"[Browse] Failed: {name} — {err}");
    }

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
            if (!SetProperty(ref _detectedMod, value))
                return;

            OnPropertyChanged(nameof(HasDetection));
            OnPropertyChanged(nameof(HasNoDetection));
            OnPropertyChanged(nameof(ShowEmptyDetection));
            OnPropertyChanged(nameof(DetectedFilesSample));
            OnPropertyChanged(nameof(HasMoreDetectedFiles));
            OnPropertyChanged(nameof(DetectedFilesMoreText));
        }
    }

    public bool IsDetecting
    {
        get => _isDetecting;
        set
        {
            if (SetProperty(ref _isDetecting, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(ShowEmptyDetection));
            }
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            if (SetProperty(ref _isInstalling, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsDetecting && !IsInstalling;
    public bool HasDetection => DetectedMod is not null;
    public bool HasNoDetection => DetectedMod is null;
    public bool ShowEmptyDetection => DetectedMod is null && !IsDetecting;

    public string AuthorOverride
    {
        get => _authorOverride;
        set => SetProperty(ref _authorOverride, value);
    }

    public string NameOverride
    {
        get => _nameOverride;
        set => SetProperty(ref _nameOverride, value);
    }

    public IEnumerable<string> DetectedFilesSample =>
        DetectedMod?.Files.Take(10) ?? [];

    public bool HasMoreDetectedFiles =>
        (DetectedMod?.Files.Count ?? 0) > 10;

    public string DetectedFilesMoreText =>
        HasMoreDetectedFiles
            ? $"+{DetectedMod!.Files.Count - 10} more files"
            : "";

    public ICommand BrowseCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand ClearCommand { get; }

    public async Task DetectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        DroppedPath = path;
        DetectedMod = null;
        IsDetecting = true;

        try
        {
            var detected = await Task.Run(() => _detector.Detect(path));
            detected.Name = string.IsNullOrWhiteSpace(NameOverride) ? detected.Name : NameOverride;
            detected.Author = string.IsNullOrWhiteSpace(AuthorOverride) ? detected.Author : AuthorOverride;

            UiDispatcher.Invoke(() =>
            {
                DetectedMod = detected;
                NameOverride = detected.Name;
                AuthorOverride = detected.Author ?? "";
                AddLog($"→ {detected.TypeLabel} (confidence: {detected.ConfidenceLabel})");
            });

            // Auto-install if enabled and confidence is high
            if (AppConfig.Instance.AutoInstallHighConfidence && detected.Confidence >= 0.75f)
            {
                AddLog($"Auto-queuing high-confidence mod: {detected.Name}");
                _queue.Enqueue(detected);
                UiDispatcher.Invoke(() => DetectedMod = null);
            }
        }
        finally
        {
            UiDispatcher.Invoke(() => IsDetecting = false);
        }
    }

    /// <summary>
    /// Detects multiple archives in parallel and queues each independently.
    /// Used when the user drops several files at once onto the drop zone.
    /// </summary>
    public async Task DetectBatchAsync(IEnumerable<string> paths)
    {
        var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (validPaths.Count == 0) return;

        if (validPaths.Count == 1)
        {
            await DetectAsync(validPaths[0]);
            return;
        }

        IsDetecting = true;
        AddLog($"Detecting {validPaths.Count} mods in parallel…");

        try
        {
            var tasks = validPaths.Select(p => Task.Run(() => _detector.Detect(p)));
            var results = await Task.WhenAll(tasks);

            foreach (var detected in results)
            {
                AddLog($"→ {detected.Name} — {detected.TypeLabel} ({detected.ConfidenceLabel})");

                if (AppConfig.Instance.AutoInstallHighConfidence && detected.Confidence >= 0.75f)
                {
                    AddLog($"Auto-queuing: {detected.Name}");
                    _queue.Enqueue(detected);
                }
                else
                {
                    // For batch drops with manual confirm, queue all regardless — user
                    // already made the intent clear by dropping the files.
                    _queue.Enqueue(detected);
                    AddLog($"Queued: {detected.Name}");
                }
            }
        }
        finally
        {
            UiDispatcher.Invoke(() => IsDetecting = false);
        }
    }

    private async Task InstallAsync()
    {
        if (DetectedMod is null)
            return;

        DetectedMod.Name = string.IsNullOrWhiteSpace(NameOverride) ? DetectedMod.Name : NameOverride.Trim();
        DetectedMod.Author = string.IsNullOrWhiteSpace(AuthorOverride) ? null : AuthorOverride.Trim();

        IsInstalling = true;
        AddLog($"Queued: {DetectedMod.Name}");
        _queue.Enqueue(DetectedMod);

        await Task.CompletedTask;
    }

    private void BrowseForArchive()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Mod Archive",
            Filter = "Mod Archives|*.zip;*.rar;*.7z|All Files|*.*",
        };

        if (dialog.ShowDialog() == true)
            _ = DetectAsync(dialog.FileName);
    }

    private void Clear()
    {
        DroppedPath = null;
        DetectedMod = null;
        AuthorOverride = "";
        NameOverride = "";
        Log.Clear();
    }

    private void AddLog(string message)
    {
        UiDispatcher.Invoke(() =>
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            LogAdded?.Invoke(message);
        });
    }
}
