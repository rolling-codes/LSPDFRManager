using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class ProfilesViewModel : ObservableObject
{
    private readonly ProfileManager _manager = ProfileManager.Instance;
    private ModProfile? _selectedProfile;
    private bool _isBusy;
    private string _statusMessage = "";

    public ObservableCollection<ModProfile> Profiles { get; } = [];
    public ObservableCollection<string> ProgressLog { get; } = [];

    public ModProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

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

    public ICommand CreateProfileCommand { get; }
    public ICommand DuplicateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }

    public ProfilesViewModel()
    {
        CreateProfileCommand = new RelayCommand(CreateProfile);
        DuplicateProfileCommand = new RelayCommand(DuplicateProfile, () => SelectedProfile != null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null);
        ApplyProfileCommand = new RelayCommand(() => _ = ApplyProfileAsync(), () => SelectedProfile != null && IsIdle);
        ExportProfileCommand = new RelayCommand(ExportProfile, () => SelectedProfile != null);
        ImportProfileCommand = new RelayCommand(ImportProfile);

        _manager.Load();
        ReloadProfiles();
    }

    private void ReloadProfiles()
    {
        Profiles.Clear();
        foreach (var p in _manager.Profiles) Profiles.Add(p);
    }

    private void CreateProfile()
    {
        var profile = _manager.Create("New Profile");
        ReloadProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        StatusMessage = $"Created: {profile.Name}";
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        var copy = _manager.Duplicate(SelectedProfile);
        ReloadProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == copy.Id);
        StatusMessage = $"Duplicated: {copy.Name}";
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        _manager.Delete(SelectedProfile);
        ReloadProfiles();
        SelectedProfile = null;
        StatusMessage = "Profile deleted.";
    }

    private async Task ApplyProfileAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        ProgressLog.Clear();
        StatusMessage = $"Applying: {SelectedProfile.Name}…";

        var progress = new Progress<string>(msg => Core.UiDispatcher.Invoke(() =>
        {
            ProgressLog.Add(msg);
            StatusMessage = msg;
        }));

        try
        {
            await _manager.ApplyAsync(SelectedProfile, progress);
            StatusMessage = $"Profile applied: {SelectedProfile.Name}";
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

    private void ExportProfile()
    {
        if (SelectedProfile is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Profile",
            Filter = "Profile|*.json",
            FileName = SelectedProfile.Name,
        };
        if (dialog.ShowDialog() == true)
        {
            _manager.Export(SelectedProfile, dialog.FileName);
            StatusMessage = $"Exported: {dialog.FileName}";
        }
    }

    private void ImportProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import Profile", Filter = "Profile|*.json" };
        if (dialog.ShowDialog() != true) return;
        var profile = _manager.Import(dialog.FileName);
        if (profile is null) { StatusMessage = "Import failed."; return; }
        ReloadProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        StatusMessage = $"Imported: {profile.Name}";
    }
}
