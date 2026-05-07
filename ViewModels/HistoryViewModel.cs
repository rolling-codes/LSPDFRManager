using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class HistoryViewModel : ObservableObject
{
    private readonly ChangeHistoryService _service = ChangeHistoryService.Instance;
    private string _searchText = "";
    private string _selectedAction = "All";
    private ChangeHistoryEntry? _selectedEntry;

    public ObservableCollection<ChangeHistoryEntry> Entries { get; } = [];
    public ObservableCollection<string> ActionFilters { get; } = ["All",
        "Installed", "Uninstalled", "Enabled", "Disabled",
        "ProfileApplied", "SafeLaunchApplied", "BackupCreated",
        "RestorePerformed", "ScanPerformed", "OperationFailed"];

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilters(); }
    }

    public string SelectedAction
    {
        get => _selectedAction;
        set { if (SetProperty(ref _selectedAction, value)) ApplyFilters(); }
    }

    public ChangeHistoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public ICommand ClearHistoryCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportTxtCommand { get; }
    public ICommand RefreshCommand { get; }

    public HistoryViewModel()
    {
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        ExportJsonCommand = new RelayCommand(() => _ = ExportAsync(true));
        ExportTxtCommand = new RelayCommand(() => _ = ExportAsync(false));
        RefreshCommand = new RelayCommand(Refresh);

        _service.Load();
        Refresh();
    }

    private void Refresh()
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        Entries.Clear();
        ChangeHistoryAction? action = SelectedAction == "All" ? null
            : Enum.TryParse<ChangeHistoryAction>(SelectedAction, out var a) ? a : null;

        var filtered = _service.Filter(action, search: SearchText.Length > 0 ? SearchText : null);
        foreach (var e in filtered) Entries.Add(e);
    }

    private void ClearHistory()
    {
        _service.Clear();
        ApplyFilters();
    }

    private async Task ExportAsync(bool asJson)
    {
        var ext = asJson ? ".json" : ".txt";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export History",
            Filter = asJson ? "JSON|*.json" : "Text|*.txt",
            FileName = $"change_history_{DateTime.Now:yyyyMMdd}",
        };
        if (dialog.ShowDialog() == true)
            await _service.ExportAsync(dialog.FileName, asJson);
    }
}
