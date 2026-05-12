using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class ModConfigViewModel : ObservableObject
{
    private readonly ModConfigService _service = new();

    private ObservableCollection<ModConfigFile> _configFiles = [];
    private ModConfigFile? _selectedFile;
    private string _editorContent = "";
    private string _statusMessage = "";
    private bool _isModified;
    private bool _hasValidationError;
    private string? _validationError;

    public ObservableCollection<ModConfigFile> ConfigFiles
    {
        get => _configFiles;
        set => SetProperty(ref _configFiles, value);
    }

    public ModConfigFile? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
                OnSelectedFileChanged();
        }
    }

    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (SetProperty(ref _editorContent, value))
                OnEditorContentChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    public bool HasValidationError
    {
        get => _hasValidationError;
        set => SetProperty(ref _hasValidationError, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand ClearCommand { get; }

    public ModConfigViewModel()
    {
        BrowseCommand = new RelayCommand(BrowseForFile);
        SaveCommand = new RelayCommand(SaveFile, () => IsModified && !HasValidationError);
        ValidateCommand = new RelayCommand(RunValidate);
        ReloadCommand = new RelayCommand(ReloadFile, () => SelectedFile is not null);
        ClearCommand = new RelayCommand(ClearAll);
    }

    private void OnSelectedFileChanged()
    {
        if (_selectedFile is null)
        {
            UiDispatcher.Invoke(() =>
            {
                EditorContent = "";
                IsModified = false;
                HasValidationError = false;
                ValidationError = null;
                StatusMessage = "";
            });
            return;
        }

        UiDispatcher.Invoke(() =>
        {
            EditorContent = _selectedFile.Content;
            IsModified = false;
            HasValidationError = !_selectedFile.IsValid;
            ValidationError = _selectedFile.ValidationError;
            StatusMessage = $"Loaded: {_selectedFile.FileName}";
        });
    }

    private void OnEditorContentChanged()
    {
        if (_selectedFile is null) return;

        IsModified = EditorContent != _selectedFile.Content;
        RunValidate();
    }

    private void RunValidate()
    {
        var (isValid, error) = _service.ValidateXml(EditorContent);
        UiDispatcher.Invoke(() =>
        {
            HasValidationError = !isValid;
            ValidationError = error;
            if (isValid)
                StatusMessage = "XML is valid.";
            else
                StatusMessage = $"Validation error: {error}";
        });

        AppLogger.Info($"[MOD_CONFIG_VALIDATE] HasError={!isValid}");
    }

    private void SaveFile()
    {
        if (_selectedFile is null) return;

        _selectedFile.Content = EditorContent;
        var success = _service.SaveFile(_selectedFile);

        UiDispatcher.Invoke(() =>
        {
            if (success)
            {
                IsModified = false;
                StatusMessage = $"Saved: {_selectedFile.FileName}";
            }
            else
            {
                StatusMessage = "Save failed — check validation errors.";
            }
        });
    }

    private void ReloadFile()
    {
        if (_selectedFile is null) return;

        try
        {
            var reloaded = _service.LoadFile(_selectedFile.FilePath);
            var idx = ConfigFiles.IndexOf(_selectedFile);
            UiDispatcher.Invoke(() =>
            {
                if (idx >= 0)
                    ConfigFiles[idx] = reloaded;
                SelectedFile = reloaded;
                StatusMessage = $"Reloaded: {reloaded.FileName}";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[MOD_CONFIG_ERROR] Reload failed", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Reload failed: {ex.Message}");
        }
    }

    private void ClearAll()
    {
        UiDispatcher.Invoke(() =>
        {
            ConfigFiles.Clear();
            SelectedFile = null;
            EditorContent = "";
            IsModified = false;
            HasValidationError = false;
            ValidationError = null;
            StatusMessage = "Cleared.";
        });
    }

    /// <summary>
    /// Called from the view's code-behind after the user picks a file via OpenFileDialog.
    /// </summary>
    public void AddFileFromPath(string path)
    {
        if (ConfigFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Already open: {Path.GetFileName(path)}";
            return;
        }

        try
        {
            var file = _service.LoadFile(path);
            UiDispatcher.Invoke(() =>
            {
                ConfigFiles.Add(file);
                SelectedFile = file;
                StatusMessage = $"Opened: {file.FileName}";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[MOD_CONFIG_ERROR] AddFile failed: {path}", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Failed to open file: {ex.Message}");
        }
    }

    // Called by BrowseCommand — the view-side dialog is wired in code-behind,
    // but we expose this for testability / pure-VM callers.
    private void BrowseForFile()
    {
        // Signal the view to open the dialog. The view subscribes via BrowseRequested.
        BrowseRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? BrowseRequested;
}
