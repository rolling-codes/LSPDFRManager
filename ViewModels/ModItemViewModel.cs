using System.Windows.Input;
using System.Windows.Media;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class ModItemViewModel : ObservableObject
{
    private readonly InstalledMod _mod;
    private readonly ModLibraryService _library = ModLibraryService.Instance;
    private bool _isInstalling;
    private bool _hasError;
    private string? _errorMessage;
    private Brush _statusBrush;

    public ICommand ToggleCommand { get; }
    public ICommand UninstallCommand { get; }

    public ModItemViewModel(InstalledMod mod)
    {
        _mod = mod;
        _statusBrush = CreateStatusBrush();

        ToggleCommand = new RelayCommand(() =>
        {
            IsEnabled = !IsEnabled;
            _library.SetEnabled(_mod.Id, IsEnabled);
        });

        UninstallCommand = new RelayCommand(() =>
        {
            // Re-enable files so they exist under their original names before deletion
            if (!_mod.IsEnabled) _library.SetEnabled(_mod.Id, true);

            foreach (var file in _mod.InstalledFiles)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                    if (File.Exists(file + ".disabled")) File.Delete(file + ".disabled");
                }
                catch { /* best-effort */ }
            }

            // Remove DLC entry from dlclist.xml if applicable
            if (_mod.Type == ModType.VehicleDlc && !string.IsNullOrEmpty(_mod.DlcPackName))
                DlcListService.RemoveEntry(_mod.DlcPackName);

            _library.Remove(_mod.Id);
        });
    }

    private Brush CreateStatusBrush()
    {
        var color = IsEnabled
            ? Color.FromArgb(255, 16, 185, 129)      // #10B981 (success)
            : Color.FromArgb(255, 61, 79, 106);      // #3D4F6A (muted)
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public InstalledMod Model => _mod;

    public string Name => _mod.Name;
    public string Author => _mod.Author;
    public string Version => _mod.Version;
    public string TypeLabel => _mod.TypeLabel;
    public string TypeColor => _mod.TypeColor;
    public DateTime InstalledAt => _mod.InstalledAt;
    public string DlcPackName => _mod.DlcPackName;
    public List<string> InstalledFiles => _mod.InstalledFiles;

    public bool IsEnabled
    {
        get => _mod.IsEnabled;
        set
        {
            if (_mod.IsEnabled != value)
            {
                _mod.IsEnabled = value;
                _statusBrush = CreateStatusBrush();
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool HasConflict
    {
        get => _mod.HasConflict;
        set
        {
            if (_mod.HasConflict != value)
            {
                _mod.HasConflict = value;
                OnPropertyChanged(nameof(HasConflict));
            }
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            if (SetProperty(ref _isInstalling, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public int DetectionScore
    {
        get => _mod.DetectionScore;
        set => _mod.DetectionScore = value;
    }

    // ── Computed UI properties ──────────────────────────────────────

    public double Opacity => IsEnabled ? 1.0 : 0.6;

    public Brush StatusBrush => _statusBrush;

    public string StatusText => HasError
        ? "Error"
        : IsInstalling
            ? "Installing…"
            : IsEnabled
                ? "Enabled"
                : "Disabled";

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    public Guid Id => _mod.Id;
}
