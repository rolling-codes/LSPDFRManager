using System.Windows.Input;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class KeysViewModel : ObservableObject
{
    private readonly KeyManagerService _keyManager = KeyManagerService.Instance;
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    private ModKey? _selectedKey;
    private string _manualModName = "";
    private string _manualFileName = "";
    private string _manualContent = "";
    private string _statusMessage = "";

    public ObservableCollection<ModKey> Keys => _keyManager.Keys;

    public ModKey? SelectedKey
    {
        get => _selectedKey;
        set => SetProperty(ref _selectedKey, value);
    }

    public string ManualModName
    {
        get => _manualModName;
        set => SetProperty(ref _manualModName, value);
    }

    public string ManualFileName
    {
        get => _manualFileName;
        set => SetProperty(ref _manualFileName, value);
    }

    public string ManualContent
    {
        get => _manualContent;
        set => SetProperty(ref _manualContent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<InstalledMod> InstalledMods => _library.Mods;

    public ICommand AddFromFileCommand { get; }
    public ICommand AddManuallyCommand { get; }
    public ICommand DeleteKeyCommand { get; }
    public ICommand ApplyToModCommand { get; }

    public KeysViewModel()
    {
        AddFromFileCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Key File",
                Filter = "Key Files|*.key;*.dat;*.txt|All Files|*.*",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var key = _keyManager.AddKeyFromFile(dlg.FileName);
                StatusMessage = $"Key '{key.KeyFileName}' added.";
            }
            catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        });

        AddManuallyCommand = new RelayCommand(
            () =>
            {
                if (string.IsNullOrWhiteSpace(_manualFileName) ||
                    string.IsNullOrWhiteSpace(_manualContent))
                {
                    StatusMessage = "File name and content are required.";
                    return;
                }
                _keyManager.AddKeyManually(_manualModName, _manualFileName, _manualContent);
                ManualModName = ManualFileName = ManualContent = "";
                StatusMessage = "Key added.";
            },
            () => !string.IsNullOrWhiteSpace(_manualFileName));

        DeleteKeyCommand = new RelayCommand(
            obj =>
            {
                var key = obj as ModKey ?? _selectedKey;
                if (key is null) return;
                _keyManager.DeleteKey(key.Id);
                if (SelectedKey?.Id == key.Id) SelectedKey = null;
                StatusMessage = "Key deleted.";
            });

        ApplyToModCommand = new RelayCommand(
            obj =>
            {
                if (_selectedKey is null || obj is not InstalledMod mod) return;
                var ok = _keyManager.ApplyKeyToMod(_selectedKey, mod);
                StatusMessage = ok
                    ? $"Key applied to {mod.Name}."
                    : "Failed to apply key — check mod install path.";
            },
            obj => _selectedKey is not null && obj is InstalledMod);
    }
}
