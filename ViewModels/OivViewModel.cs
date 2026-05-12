using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// ViewModel for the OIV Creator and Installer.
/// Toggle between modes with <see cref="IsCreatorMode"/>.
/// </summary>
public class OivViewModel : ObservableObject
{
    private bool _isCreatorMode = true;
    private bool _isWorking;
    private string _statusMessage = "";

    // ── Creator fields ────────────────────────────────────────────────────────
    private string _name = "";
    private string _version = "1.0";
    private string _author = "";
    private string _description = "";
    private string _outputPath = "";

    // ── Installer fields ──────────────────────────────────────────────────────
    private string _selectedOivPath = "";
    private OivPackage? _parsedPackage;
    private string _targetRoot = "";

    public OivViewModel()
    {
        // Creator commands
        AddFileCommand       = new RelayCommand(AddFile);
        RemoveFileCommand    = new RelayCommand<OivFileEntry>(RemoveFile);
        BrowseOutputCommand  = new RelayCommand(BrowseOutput);
        CreatePackageCommand = new RelayCommand(
            () => _ = CreatePackageAsync(),
            () => CanCreate);

        // Installer commands
        BrowseOivCommand = new RelayCommand(BrowseOiv);
        PreviewCommand   = new RelayCommand(
            () => _ = PreviewAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
        InstallCommand   = new RelayCommand(
            () => _ = InstallAsync(),
            () => _parsedPackage is { IsValid: true } && !IsWorking);
    }

    // ── Mode ──────────────────────────────────────────────────────────────────

    public bool IsCreatorMode
    {
        get => _isCreatorMode;
        set
        {
            if (SetProperty(ref _isCreatorMode, value))
                OnPropertyChanged(nameof(IsInstallerMode));
        }
    }

    public bool IsInstallerMode => !_isCreatorMode;

    // ── Shared ────────────────────────────────────────────────────────────────

    public bool IsWorking
    {
        get => _isWorking;
        private set => SetProperty(ref _isWorking, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Creator properties ────────────────────────────────────────────────────

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(CanCreate));
        }
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string Author
    {
        get => _author;
        set => SetProperty(ref _author, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
                OnPropertyChanged(nameof(CanCreate));
        }
    }

    public ObservableCollection<OivFileEntry> Files { get; } = [];

    public bool CanCreate =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(OutputPath) &&
        Files.Count > 0 &&
        !IsWorking;

    // ── Creator commands ──────────────────────────────────────────────────────

    public ICommand AddFileCommand { get; }
    public ICommand RemoveFileCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand CreatePackageCommand { get; }

    private void AddFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select File to Include in OIV Package",
            Filter = "All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (var filePath in dialog.FileNames)
        {
            var fileName = Path.GetFileName(filePath);
            Files.Add(new OivFileEntry
            {
                SourcePath = filePath,
                InstallPath = fileName,
                Action = OivFileAction.Add
            });
        }

        OnPropertyChanged(nameof(CanCreate));
    }

    private void RemoveFile(OivFileEntry? entry)
    {
        if (entry is not null)
        {
            Files.Remove(entry);
            OnPropertyChanged(nameof(CanCreate));
        }
    }

    private void BrowseOutput()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save OIV Package",
            Filter = "OIV Package|*.oiv",
            DefaultExt = ".oiv",
            FileName = string.IsNullOrWhiteSpace(Name) ? "package" : Name
        };

        if (dialog.ShowDialog() == true)
            OutputPath = dialog.FileName;
    }

    private async Task CreatePackageAsync()
    {
        if (!CanCreate) return;

        IsWorking = true;
        StatusMessage = "Creating package...";

        try
        {
            var pkg = new OivPackage
            {
                Name = Name,
                Version = Version,
                Author = Author,
                Description = Description,
                Files = Files.ToList()
            };

            var success = await Task.Run(() => OivService.CreatePackage(pkg, OutputPath));

            UiDispatcher.Invoke(() =>
            {
                StatusMessage = success
                    ? $"Package created: {Path.GetFileName(OutputPath)}"
                    : $"Failed: {pkg.ValidationError ?? "Unknown error"}";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_CREATE] Unexpected error in CreatePackageAsync", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() =>
            {
                IsWorking = false;
                OnPropertyChanged(nameof(CanCreate));
            });
        }
    }

    // ── Installer properties ──────────────────────────────────────────────────

    public string SelectedOivPath
    {
        get => _selectedOivPath;
        set => SetProperty(ref _selectedOivPath, value);
    }

    public string TargetRoot
    {
        get => _targetRoot;
        set => SetProperty(ref _targetRoot, value);
    }

    public OivPackage? ParsedPackage
    {
        get => _parsedPackage;
        private set
        {
            if (SetProperty(ref _parsedPackage, value))
            {
                OnPropertyChanged(nameof(HasParsedPackage));
                OnPropertyChanged(nameof(ParseError));
            }
        }
    }

    public bool HasParsedPackage => _parsedPackage is { IsValid: true };
    public string? ParseError => _parsedPackage is { IsValid: false } ? _parsedPackage.ValidationError : null;

    public ObservableCollection<OivFileEntry> PreviewEntries { get; } = [];

    // ── Installer commands ────────────────────────────────────────────────────

    public ICommand BrowseOivCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand InstallCommand { get; }

    private void BrowseOiv()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select OIV Package",
            Filter = "OIV Packages|*.oiv|All Files|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        SelectedOivPath = dialog.FileName;
        PreviewEntries.Clear();
        ParsedPackage = null;
        StatusMessage = "Parsing package...";

        try
        {
            ParsedPackage = OivService.ParsePackage(SelectedOivPath);
            StatusMessage = ParsedPackage.IsValid
                ? $"Loaded: {ParsedPackage.Name} v{ParsedPackage.Version}"
                : $"Invalid: {ParsedPackage.ValidationError}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_PARSE] Unexpected parse error", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task PreviewAsync()
    {
        if (_parsedPackage is null || !_parsedPackage.IsValid) return;

        IsWorking = true;
        StatusMessage = "Calculating preview...";

        try
        {
            var root = string.IsNullOrWhiteSpace(TargetRoot)
                ? AppConfig.Instance.GtaPath
                : TargetRoot;

            var entries = await Task.Run(() => OivService.PreviewInstall(_parsedPackage, root));

            UiDispatcher.Invoke(() =>
            {
                PreviewEntries.Clear();
                foreach (var e in entries)
                    PreviewEntries.Add(e);

                StatusMessage = $"Preview: {entries.Count(e => e.Action == OivFileAction.Add)} add, " +
                                $"{entries.Count(e => e.Action == OivFileAction.Replace)} replace";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_INSTALL] Preview error", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Preview error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsWorking = false);
        }
    }

    private async Task InstallAsync()
    {
        if (_parsedPackage is null || !_parsedPackage.IsValid) return;

        IsWorking = true;
        StatusMessage = "Installing...";

        try
        {
            var root = string.IsNullOrWhiteSpace(TargetRoot)
                ? AppConfig.Instance.GtaPath
                : TargetRoot;

            var result = await OivService.InstallPackage(_parsedPackage, root);

            UiDispatcher.Invoke(() =>
            {
                StatusMessage = result.Success
                    ? $"Installed {result.FilesWritten} file(s) successfully."
                    : $"Install failed: {result.Error}";

                if (result.Success)
                {
                    // Refresh preview to show updated state
                    _ = PreviewAsync();
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_INSTALL] Unexpected install error", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Error: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() => IsWorking = false);
        }
    }
}
