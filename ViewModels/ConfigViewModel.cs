using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// Manages plugin configuration files (.ini, .xml, .cfg, .meta, .json) for installed mods.
/// Replaces the old "Keys" tab — LSPDFR has no software key system; this instead
/// lets users view, edit, and snapshot the config files their mods use.
/// HTML/HTM files are intentionally excluded: no GTA V plugin uses HTML config files,
/// and the raw editor is a plain TextBox (no script execution risk), but admitting
/// arbitrary HTML would be misleading — users should open those in a browser.
/// </summary>
public class ConfigViewModel : ObservableObject
{
    private readonly ConfigManagerService _configs = ConfigManagerService.Instance;
    private ConfigEntry? _selectedConfig;
    private string _editContent = "";
    private string _statusMessage = "";
    private bool _showRawEditor = false;
    private string _entrySearch = "";
    private IniConfigEntry? _selectedEntry;
    private List<IniConfigEntry> _allParsedEntries = [];

    public ObservableCollection<ConfigEntry> Configs => _configs.Configs;
    public ObservableCollection<IniConfigEntry> ParsedEntries { get; } = [];

    public ConfigEntry? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            SetProperty(ref _selectedConfig, value);
            EditContent = value?.ConfigContent ?? "";
            SelectedEntry = null;
            LoadParsedEntries(value);
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
            OnPropertyChanged(nameof(ShowParsedEditor));
            OnPropertyChanged(nameof(ShowRawEditor));
        }
    }

    public IniConfigEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            SetProperty(ref _selectedEntry, value);
            OnPropertyChanged(nameof(HasEntrySelection));
        }
    }

    public string EntrySearch
    {
        get => _entrySearch;
        set
        {
            if (SetProperty(ref _entrySearch, value))
                RefreshParsedFilter();
        }
    }

    /// <summary>Editable copy of the selected config's content (raw mode).</summary>
    public string EditContent
    {
        get => _editContent;
        set => SetProperty(ref _editContent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool ShowRawEditorToggle
    {
        get => _showRawEditor;
        set
        {
            if (SetProperty(ref _showRawEditor, value))
            {
                OnPropertyChanged(nameof(ShowParsedEditor));
                OnPropertyChanged(nameof(ShowRawEditor));
            }
        }
    }

    public bool ShowParsedEditor => HasSelection && !_showRawEditor && IsIniFile;
    public bool ShowRawEditor    => HasSelection && (_showRawEditor || !IsIniFile);

    public bool IsIniFile =>
        _selectedConfig?.ConfigFileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) == true;

    public bool HasSelection    => _selectedConfig is not null;
    public bool HasNoSelection  => _selectedConfig is null;
    public bool HasEntrySelection => _selectedEntry is not null;

    public ICommand SaveEntryCommand       { get; }
    public ICommand ToggleRawEditorCommand { get; }

    public ICommand AddFromFileCommand    { get; }
    public ICommand SaveEditCommand       { get; }
    public ICommand DeleteConfigCommand   { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand ExtractFromModCommand { get; }

    public ConfigViewModel()
    {
        ToggleRawEditorCommand = new RelayCommand(() =>
        {
            ShowRawEditorToggle = !ShowRawEditorToggle;
        });

        SaveEntryCommand = new RelayCommand(
            () =>
            {
                if (_selectedEntry is null || _selectedConfig is null) return;
                if (string.IsNullOrEmpty(_selectedEntry.FilePath) || !File.Exists(_selectedEntry.FilePath))
                {
                    StatusMessage = "Source file not found — cannot save individual key.";
                    return;
                }
                if (ParsedIniService.SaveEntry(_selectedEntry))
                {
                    _selectedEntry.RawValue = _selectedEntry.EditValue;
                    _selectedEntry.IsDirty = false;
                    StatusMessage = $"Saved {_selectedEntry.Key} = {_selectedEntry.EditValue}";
                }
                else
                {
                    StatusMessage = $"Save failed for key '{_selectedEntry.Key}'. Check app.log.";
                }
            },
            () => _selectedEntry is not null && _selectedEntry.IsDirty);

        AddFromFileCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select plugin config file",
                Filter = "Config files|*.ini;*.xml;*.cfg;*.meta;*.json|All files|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            var content = File.ReadAllText(dlg.FileName);
            var modName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

            _configs.AddBuiltInConfig(modName, System.IO.Path.GetFileName(dlg.FileName),
                content, dlg.FileName);

            StatusMessage = $"Added config: {System.IO.Path.GetFileName(dlg.FileName)}";
        });

        SaveEditCommand = new RelayCommand(
            () =>
            {
                if (_selectedConfig is null) return;
                var validationError = ValidateConfigContent(_selectedConfig.ConfigFileName, EditContent);
                if (validationError is not null)
                {
                    StatusMessage = validationError;
                    return;
                }

                // Write back to source file if it still exists
                if (!string.IsNullOrEmpty(_selectedConfig.SourcePath) &&
                    File.Exists(_selectedConfig.SourcePath))
                {
                    try
                    {
                        WriteSourceConfigWithRollback(_selectedConfig.SourcePath, EditContent);
                        _configs.UpdateConfig(_selectedConfig.Id, EditContent);
                        StatusMessage = $"Saved to {System.IO.Path.GetFileName(_selectedConfig.SourcePath)}";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Save failed and was rolled back: {ex.Message}";
                    }
                }
                else
                {
                    _configs.UpdateConfig(_selectedConfig.Id, EditContent);
                    StatusMessage = "Config snapshot updated.";
                }
            },
            () => HasSelection);

        DeleteConfigCommand = new RelayCommand(
            obj =>
            {
                var target = obj as ConfigEntry ?? _selectedConfig;
                if (target is null) return;
                if (SelectedConfig?.Id == target.Id) SelectedConfig = null;
                _configs.RemoveConfig(target.Id);
                StatusMessage = "Config removed.";
            },
            obj => (obj as ConfigEntry ?? _selectedConfig) is not null);

        OpenInExplorerCommand = new RelayCommand(
            () =>
            {
                if (_selectedConfig is null) return;
                var path = _selectedConfig.SourcePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"/select,\"{path}\"");
            },
            () => HasSelection && File.Exists(_selectedConfig?.SourcePath ?? ""));

        ExtractFromModCommand = new RelayCommand(() =>
        {
            // Walk each installed mod looking for config files and offer to snapshot them
            var library = ModLibraryService.Instance;
            var configExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".ini", ".xml", ".cfg", ".meta", ".json" };
            int added = 0;

            foreach (var mod in library.Mods)
            {
                foreach (var file in mod.InstalledFiles)
                {
                    if (!configExts.Contains(System.IO.Path.GetExtension(file))) continue;
                    if (!File.Exists(file)) continue;
                    // Skip if already tracked
                    if (_configs.Configs.Any(c =>
                            c.SourcePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var content = File.ReadAllText(file);
                    _configs.AddBuiltInConfig(mod.Name,
                        System.IO.Path.GetFileName(file), content, file);
                    added++;
                }
            }

            StatusMessage = added > 0
                ? $"Extracted {added} config file(s) from installed mods."
                : "No new config files found in installed mods.";
        });
    }

    private static string? ValidateConfigContent(string fileName, string content)
    {
        var extension = System.IO.Path.GetExtension(fileName);
        try
        {
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                using (JsonDocument.Parse(content)) { }
            else if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                System.Xml.Linq.XDocument.Parse(content);

            return null;
        }
        catch (Exception ex)
        {
            return $"Validation failed: {ex.Message}";
        }
    }

    private void LoadParsedEntries(ConfigEntry? config)
    {
        ParsedEntries.Clear();
        _allParsedEntries = [];
        _entrySearch = "";
        OnPropertyChanged(nameof(EntrySearch));

        if (config is null ||
            !config.ConfigFileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(config.SourcePath) ||
            !File.Exists(config.SourcePath))
            return;

        _allParsedEntries = ParsedIniService.Parse(config.SourcePath);
        foreach (var e in _allParsedEntries)
            ParsedEntries.Add(e);
    }

    private void RefreshParsedFilter()
    {
        ParsedEntries.Clear();
        var q = _entrySearch.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _allParsedEntries
            : _allParsedEntries.Where(e =>
                e.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Section.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.RawValue.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (e.Comment?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
        foreach (var e in filtered)
            ParsedEntries.Add(e);
    }

    private static void WriteSourceConfigWithRollback(string sourcePath, string content)
    {
        var directory = System.IO.Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException("Config file has no parent folder.");
        Directory.CreateDirectory(directory);

        var backupPath = $"{sourcePath}.lspdfrmanager.bak";
        var tempPath = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(sourcePath)}.{Guid.NewGuid():N}.tmp");

        File.Copy(sourcePath, backupPath, overwrite: true);
        File.WriteAllText(tempPath, content);

        try
        {
            File.Move(tempPath, sourcePath, overwrite: true);
            File.Delete(backupPath);
        }
        catch
        {
            if (File.Exists(backupPath))
                File.Move(backupPath, sourcePath, overwrite: true);
            throw;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
