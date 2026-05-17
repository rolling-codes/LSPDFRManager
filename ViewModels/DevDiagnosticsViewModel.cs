using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// Developer diagnostics page — shows feature flag state, recent log tail, and
/// allows exporting a support bundle.  Only visible when dev mode is enabled.
/// </summary>
public class DevDiagnosticsViewModel : ObservableObject
{
    private readonly IFeatureFlagService _flags = FeatureFlagService.Instance;
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _logTail = string.Empty;

    public ObservableCollection<FeatureFlagRow> FeatureRows { get; } = [];
    public ObservableCollection<string> ProgressLog { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsIdle => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LogTail
    {
        get => _logTail;
        set => SetProperty(ref _logTail, value);
    }

    public ICommand RefreshLogCommand { get; }
    public ICommand ExportBundleCommand { get; }
    public ICommand ToggleFeatureCommand { get; }
    public ICommand ResetFeatureCommand { get; }

    public DevDiagnosticsViewModel()
    {
        RefreshLogCommand   = new RelayCommand(RefreshLog);
        ExportBundleCommand = new RelayCommand(async () => await ExportBundleAsync(), () => IsIdle);
        ToggleFeatureCommand = new RelayCommand<FeatureFlagRow>(ToggleFeature);
        ResetFeatureCommand  = new RelayCommand<FeatureFlagRow>(ResetFeature);

        LoadFeatureRows();
        RefreshLog();
    }

    private void LoadFeatureRows()
    {
        FeatureRows.Clear();
        foreach (var f in _flags.AllFeatures)
        {
            FeatureRows.Add(new FeatureFlagRow
            {
                Id          = f.Id,
                Title       = f.Title,
                Stage       = f.Stage.ToString(),
                IsEnabled   = _flags.IsEnabled(f.Id),
                Description = f.Description,
            });
        }
    }

    private void RefreshLog()
    {
        try
        {
            var path = AppDataPaths.LogFile;
            if (!File.Exists(path))
            {
                LogTail = "(no app.log found)";
                return;
            }
            var lines = File.ReadAllLines(path);
            LogTail = string.Join(Environment.NewLine, lines.TakeLast(100));
        }
        catch (Exception ex)
        {
            LogTail = $"Could not read log: {ex.Message}";
        }
    }

    private async Task ExportBundleAsync()
    {
        IsBusy = true;
        StatusMessage = "Exporting support bundle…";
        ProgressLog.Clear();

        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Save Support Bundle",
                Filter     = "ZIP Archive|*.zip",
                FileName   = $"LSPDFRManager-support-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            };
            if (dlg.ShowDialog() != true)
            {
                StatusMessage = "Export cancelled.";
                return;
            }

            var svc = new SupportBundleService();
            var progress = new Progress<string>(msg =>
            {
                Core.UiDispatcher.Invoke(() =>
                {
                    ProgressLog.Insert(0, msg);
                    StatusMessage = msg;
                });
            });

            var path = await svc.ExportAsync(dlg.FileName, progress);
            StatusMessage = $"Bundle saved: {path}";
            ProgressLog.Insert(0, $"Saved to: {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Core.AppLogger.Error($"[DevDiagnostics] Support bundle export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleFeature(FeatureFlagRow? row)
    {
        if (row is null) return;
        var newState = !row.IsEnabled;
        _flags.SetEnabled(row.Id, newState);
        row.IsEnabled = newState;
        OnPropertyChanged(nameof(FeatureRows));
        StatusMessage = $"Feature '{row.Id}' set to {(newState ? "enabled" : "disabled")}.";
    }

    private void ResetFeature(FeatureFlagRow? row)
    {
        if (row is null) return;
        _flags.Reset(row.Id);
        row.IsEnabled = _flags.IsEnabled(row.Id);
        OnPropertyChanged(nameof(FeatureRows));
        StatusMessage = $"Feature '{row.Id}' reset to default.";
    }
}

/// <summary>Row binding model for the feature flag data grid.</summary>
public class FeatureFlagRow : ObservableObject
{
    private bool _isEnabled;

    public string Id          { get; set; } = string.Empty;
    public string Title       { get; set; } = string.Empty;
    public string Stage       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
