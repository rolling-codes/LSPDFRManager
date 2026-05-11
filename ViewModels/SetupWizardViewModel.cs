using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class SetupWizardViewModel : ObservableObject
{
    private readonly SetupWizardService _service = new();
    private int _currentPage = 0;
    private string _gtaPath = AppConfig.Instance.GtaPath;
    private string _validationMessage = "";
    private string _backupPath = AppConfig.Instance.BackupPath;

    public ObservableCollection<GamePathCandidate> DetectedPaths { get; } = [];

    public int CurrentPage
    {
        get => _currentPage;
        set { if (SetProperty(ref _currentPage, value)) { OnPropertyChanged(nameof(PageTitle)); OnPropertyChanged(nameof(CanGoBack)); OnPropertyChanged(nameof(IsLastPage)); } }
    }

    public string PageTitle => CurrentPage switch
    {
        0 => "Welcome",
        1 => "Detect GTA V Folder",
        2 => "Validate GTA V Folder",
        3 => "Detect Dependencies",
        4 => "Backup Folder Setup",
        5 => "Browse API Setup",
        6 => "Finish",
        _ => "",
    };

    public bool CanGoBack => CurrentPage > 0;
    public bool IsLastPage => CurrentPage == 6;

    public string GtaPath
    {
        get => _gtaPath;
        set
        {
            if (!SetProperty(ref _gtaPath, value)) return;
            ValidationMessage = _service.ValidatePath(value);
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public string BackupPath
    {
        get => _backupPath;
        set => SetProperty(ref _backupPath, value);
    }

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand DetectPathsCommand { get; }
    public ICommand SelectCandidateCommand { get; }
    public ICommand BrowseGtaPathCommand { get; }
    public ICommand BrowseBackupPathCommand { get; }
    public ICommand FinishCommand { get; }

    public SetupWizardViewModel()
    {
        NextCommand = new RelayCommand(Next);
        BackCommand = new RelayCommand(Back, () => CanGoBack);
        DetectPathsCommand = new RelayCommand(DetectPaths);
        SelectCandidateCommand = new RelayCommand<GamePathCandidate?>(SelectCandidate);
        BrowseGtaPathCommand = new RelayCommand(BrowseGtaPath);
        BrowseBackupPathCommand = new RelayCommand(BrowseBackupPath);
        FinishCommand = new RelayCommand(Finish);
    }

    private void Next()
    {
        if (CurrentPage < 6) CurrentPage++;
    }

    private void Back()
    {
        if (CurrentPage > 0) CurrentPage--;
    }

    private void DetectPaths()
    {
        DetectedPaths.Clear();
        foreach (var c in _service.DetectGamePaths()) DetectedPaths.Add(c);
    }

    private void SelectCandidate(GamePathCandidate? candidate)
    {
        if (candidate is not null) GtaPath = candidate.Path;
    }

    private void BrowseGtaPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select GTA V installation folder" };
        if (dialog.ShowDialog() == true) GtaPath = dialog.FolderName;
    }

    private void BrowseBackupPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select backup folder" };
        if (dialog.ShowDialog() == true) BackupPath = dialog.FolderName;
    }

    private void Finish()
    {
        AppConfig.Instance.GtaPath = GtaPath;
        AppConfig.Instance.BackupPath = BackupPath;
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        AppConfig.Instance.Save();
        LspdfrStatusService.Instance.Refresh();
        DashboardStatusService.Instance.Refresh();
    }
}
