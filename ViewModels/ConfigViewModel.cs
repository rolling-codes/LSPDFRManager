using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// Manages plugin configuration files (.ini, .xml, .cfg, .meta) for installed mods.
/// Replaces the old "Keys" tab — LSPDFR has no software key system; this instead
/// lets users view, edit, and snapshot the config files their mods use.
/// </summary>
public class ConfigViewModel : ObservableObject
{
    private readonly ConfigManagerService _configs = ConfigManagerService.Instance;
    private ConfigEntry? _selectedConfig;
    private string _editContent = "";
    private string _statusMessage = "";

    public ObservableCollection<ConfigEntry> Configs => _configs.Configs;

    public ConfigEntry? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            SetProperty(ref _selectedConfig, value);
            EditContent = value?.ConfigContent ?? "";
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
        }
    }

    /// <summary>Editable copy of the selected config's content.</summary>
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

    public bool HasSelection   => _selectedConfig is not null;
    public bool HasNoSelection => _selectedConfig is null;

    public ICommand AddFromFileCommand    { get; }
    public ICommand SaveEditCommand       { get; }
    public ICommand DeleteConfigCommand   { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand ExtractFromModCommand { get; }

    public ConfigViewModel()
    {
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
                _configs.UpdateConfig(_selectedConfig.Id, EditContent);

                // Write back to source file if it still exists
                if (!string.IsNullOrEmpty(_selectedConfig.SourcePath) &&
                    File.Exists(_selectedConfig.SourcePath))
                {
                    File.WriteAllText(_selectedConfig.SourcePath, EditContent);
                    StatusMessage = $"Saved to {System.IO.Path.GetFileName(_selectedConfig.SourcePath)}";
                }
                else
                {
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
}
