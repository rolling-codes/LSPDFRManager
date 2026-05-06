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
                AddLog($"→ {detected.TypeLabel}");
            });
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
