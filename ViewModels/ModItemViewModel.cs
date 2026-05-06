using System.Windows.Input;
using System.Windows.Media;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class ModItemViewModel : ObservableObject
{
    private readonly InstalledMod _mod;
    private readonly ModLibraryService _library;
    private Brush _statusBrush;
    private bool _isInstalling;
    private bool _hasError;
    private string? _errorMessage;

    public ModItemViewModel(InstalledMod mod)
    {
        _mod = mod;
        _library = ModLibraryService.Instance;
        _statusBrush = CreateStatusBrush(_mod.IsEnabled);

        ToggleCommand = new RelayCommand(ToggleEnabled);
        UninstallCommand = new RelayCommand(Uninstall);
    }

    public InstalledMod Model => _mod;

    public ICommand ToggleCommand { get; }
    public ICommand UninstallCommand { get; }

    public Guid Id => _mod.Id;
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
            if (_mod.IsEnabled == value)
                return;

            _mod.IsEnabled = value;
            _statusBrush = CreateStatusBrush(value);
            NotifyVisualStateChanged();
        }
    }

    public bool HasConflict
    {
        get => _mod.HasConflict;
        set
        {
            if (_mod.HasConflict == value)
                return;

            _mod.HasConflict = value;
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(RiskSummary));
        }
    }

    public int DetectionScore
    {
        get => _mod.DetectionScore;
        set
        {
            if (_mod.DetectionScore == value)
                return;

            _mod.DetectionScore = value;
            OnPropertyChanged(nameof(DetectionScore));
            OnPropertyChanged(nameof(RiskTier));
            OnPropertyChanged(nameof(RiskBrush));
            OnPropertyChanged(nameof(RiskSummary));
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            if (SetProperty(ref _isInstalling, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (SetProperty(ref _hasError, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public double Opacity => IsEnabled ? 1.0 : 0.6;
    public Brush StatusBrush => _statusBrush;

    public string StatusText => HasError
        ? "Error"
        : IsInstalling
            ? "Installing…"
            : IsEnabled
                ? "Enabled"
                : "Disabled";

    public string RiskTier =>
        DetectionScore >= 70 ? "Safe" :
        DetectionScore >= 40 ? "Medium" :
        "High";

    public Brush RiskBrush => CreateRiskBrush(RiskTier);

    public string RiskSummary => HasConflict
        ? $"{RiskTier} • Conflicts detected"
        : $"{RiskTier} • No conflicts";

    public List<string> ConflictDetails => _library.FindConflicts(_mod);

    public string Notes
    {
        get => _mod.Notes;
        set
        {
            if (_mod.Notes == value)
                return;

            _mod.Notes = value;
            OnPropertyChanged(nameof(Notes));
            _library.SaveProxy();
        }
    }

    public void Analyze(IEnumerable<string> incomingFiles)
    {
        var incoming = incomingFiles.ToList();
        DetectionScore = LspdfrValidator.CalculateDetectionScore(incoming);

        HasConflict = _library.Mods
            .Where(mod => mod.Id != _mod.Id)
            .Any(mod => mod.InstalledFiles.Intersect(incoming, StringComparer.OrdinalIgnoreCase).Any());

        OnPropertyChanged(nameof(ConflictDetails));
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

    private void ToggleEnabled()
    {
        _library.SetEnabled(_mod.Id, !_mod.IsEnabled);
        IsEnabled = _mod.IsEnabled;
    }

    private void Uninstall()
    {
        if (AppConfig.Instance.ConfirmBeforeUninstall)
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to uninstall '{Name}'?",
                "Confirm Uninstall",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;
        }

        _library.Uninstall(_mod.Id);
    }

    private void NotifyVisualStateChanged()
    {
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusText));
    }

    private static Brush CreateStatusBrush(bool isEnabled)
    {
        var color = isEnabled
            ? Color.FromRgb(16, 185, 129)
            : Color.FromRgb(61, 79, 106);

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Brush CreateRiskBrush(string riskTier)
    {
        var color = riskTier switch
        {
            "Safe" => Color.FromRgb(16, 185, 129),
            "Medium" => Color.FromRgb(245, 158, 11),
            _ => Color.FromRgb(239, 68, 68),
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
