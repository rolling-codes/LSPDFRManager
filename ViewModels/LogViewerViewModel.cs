using System.Windows.Input;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class LogViewerViewModel : ObservableObject
{
    private readonly LogViewerService _service = new();
    private LogViewerService.LogFile? _selectedLog;
    private string _searchText = "";
    private string _severityFilter = "";

    public ObservableCollection<LogViewerService.LogFile> AvailableLogs { get; } = [];
    public ObservableCollection<string> Lines { get; } = [];

    public LogViewerService.LogFile? SelectedLog
    {
        get => _selectedLog;
        set { if (SetProperty(ref _selectedLog, value)) LoadSelectedLog(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public string SeverityFilter
    {
        get => _severityFilter;
        set { if (SetProperty(ref _severityFilter, value)) ApplyFilter(); }
    }

    private string[] _rawLines = [];

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ClearManagerLogCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public LogViewerViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        ExportCommand = new RelayCommand(Export);
        ClearManagerLogCommand = new RelayCommand(ClearManagerLog);
        OpenFolderCommand = new RelayCommand(OpenFolder);

        Refresh();
    }

    private void Refresh()
    {
        AvailableLogs.Clear();
        foreach (var log in _service.GetAvailableLogs()) AvailableLogs.Add(log);
        if (SelectedLog is not null) LoadSelectedLog();
    }

    private void LoadSelectedLog()
    {
        if (SelectedLog is null) return;
        _rawLines = _service.ReadLines(SelectedLog.Path);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Lines.Clear();
        var filtered = _service.Search(_rawLines, SearchText, SeverityFilter.Length > 0 ? SeverityFilter : null);
        foreach (var line in filtered.TakeLast(500)) Lines.Add(line);
    }

    private void Export()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export Log", Filter = "Text|*.txt", FileName = SelectedLog?.Label ?? "log" };
        if (dialog.ShowDialog() != true) return;
        _ = _service.ExportAsync(Lines.ToArray(), dialog.FileName);
    }

    private void ClearManagerLog()
    {
        _service.ClearManagerLog();
        Refresh();
    }

    private void OpenFolder()
    {
        if (SelectedLog is null) return;
        var dir = Path.GetDirectoryName(SelectedLog.Path);
        if (dir is not null && Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }
}
