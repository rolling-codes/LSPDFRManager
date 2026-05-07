using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class DiagnosticsViewModel : ObservableObject
{
    private readonly DiagnosticsOrchestrator _orchestrator = new();
    private bool _isBusy;
    private string _selectedCategory = "All";
    private DiagnosticFinding? _selectedFinding;
    private string _statusMessage = "Ready to scan.";
    private string _severityFilter = "All";

    public ObservableCollection<DiagnosticFinding> Findings { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All", "Plugin Health", "Dependencies", "Conflicts", "Storage"];
    public ObservableCollection<string> SeverityFilters { get; } = ["All", "Ok", "Info", "Warning", "Error", "Critical"];
    public ObservableCollection<string> ProgressLog { get; } = [];

    private List<DiagnosticFinding> _allFindings = [];

    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsIdle => !IsBusy;

    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) ApplyFilters(); }
    }

    public string SeverityFilter
    {
        get => _severityFilter;
        set { if (SetProperty(ref _severityFilter, value)) ApplyFilters(); }
    }

    public DiagnosticFinding? SelectedFinding
    {
        get => _selectedFinding;
        set => SetProperty(ref _selectedFinding, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RunScanCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportTxtCommand { get; }
    public ICommand ExportHtmlCommand { get; }

    public DiagnosticsViewModel()
    {
        RunScanCommand = new RelayCommand(() => _ = RunScanAsync(), () => IsIdle);
        ExportJsonCommand = new RelayCommand(ExportJson, () => _allFindings.Count > 0);
        ExportTxtCommand = new RelayCommand(ExportTxt, () => _allFindings.Count > 0);
        ExportHtmlCommand = new RelayCommand(ExportHtml, () => _allFindings.Count > 0);
    }

    private async Task RunScanAsync()
    {
        IsBusy = true;
        ProgressLog.Clear();
        Findings.Clear();
        _allFindings.Clear();
        StatusMessage = "Scanning…";

        var progress = new Progress<string>(msg =>
        {
            Core.UiDispatcher.Invoke(() =>
            {
                ProgressLog.Add(msg);
                StatusMessage = msg;
            });
        });

        try
        {
            _allFindings = await _orchestrator.RunAllAsync(progress);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        Findings.Clear();
        var filtered = _allFindings.AsEnumerable();

        if (SelectedCategory != "All")
            filtered = filtered.Where(f => f.Category == SelectedCategory);

        if (SeverityFilter != "All" && Enum.TryParse<DiagnosticSeverity>(SeverityFilter, out var sev))
            filtered = filtered.Where(f => f.Severity == sev);

        foreach (var f in filtered)
            Findings.Add(f);
    }

    private void ExportJson() => ExportReport(".json");
    private void ExportTxt() => ExportReport(".txt");
    private void ExportHtml() => ExportReport(".html");

    private void ExportReport(string ext)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Diagnostics Report",
            Filter = ext == ".json" ? "JSON|*.json" : ext == ".html" ? "HTML|*.html" : "Text|*.txt",
            FileName = $"diagnostics_report_{DateTime.Now:yyyyMMdd}",
        };
        if (dialog.ShowDialog() != true) return;
        _ = _orchestrator.ExportReportAsync(_allFindings, dialog.FileName);
    }
}
