using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class PresetPatchService
{
    private readonly string _gtaPath;

    public PresetPatchService(string gtaPath)
    {
        _gtaPath = gtaPath;
    }

    public static IReadOnlyList<PatrolSetupPreset> BuiltInPresets { get; } =
    [
        new PatrolSetupPreset
        {
            PresetId = "controller_ultimate_backup",
            DisplayName = "Controller: Ultimate Backup on Right Thumb",
            Description = "Bind Ultimate Backup menu to right thumbstick and disable default LSPDFR backup menu.",
            Rules =
            [
                new PresetPatchRule
                {
                    File = "plugins/lspdfr/keys.ini",
                    MatchKeys = ["BackupMenu", "BackupMenuKey", "OpenBackupMenu"],
                    SetValue = "None",
                    Reason = "Disable default LSPDFR backup menu when Ultimate Backup is installed.",
                },
                new PresetPatchRule
                {
                    File = "plugins/lspdfr/UltimateBackup.ini",
                    MatchKeys = ["MenuKey", "BackupMenuKey", "MainKey", "ControllerKey"],
                    SetValue = "RightThumb",
                    Reason = "Bind Ultimate Backup menu to right thumbstick press.",
                },
            ],
        },
        new PatrolSetupPreset
        {
            PresetId = "keyboard_mouse_default",
            DisplayName = "Keyboard + Mouse Default",
            Description = "Warn on duplicate keys but preserve common defaults.",
            Rules = [],
        },
        new PatrolSetupPreset
        {
            PresetId = "stop_the_ped_compatibility",
            DisplayName = "Stop The Ped Compatibility",
            Description = "Separate Stop The Ped menu keys from LSPDFR interaction keys.",
            Rules =
            [
                new PresetPatchRule
                {
                    File = "plugins/lspdfr/StopThePed.ini",
                    MatchKeys = ["InteractionKey", "MenuKey", "MainKey"],
                    SetValue = "E",
                    Reason = "Avoid conflict with LSPDFR interaction key.",
                },
            ],
        },
    ];

    public List<IniPatchPreview> Preview(PatrolSetupPreset preset)
    {
        var previews = new List<IniPatchPreview>();
        foreach (var (filePath, rules) in GroupRulesByFile(preset))
        {
            if (!File.Exists(filePath))
                continue;
            previews.AddRange(IniParser.PreviewPatch(filePath, rules));
        }
        return previews;
    }

    public bool Apply(PatrolSetupPreset preset, bool backupFirst = true)
    {
        bool allOk = true;
        foreach (var (filePath, rules) in GroupRulesByFile(preset))
        {
            if (!File.Exists(filePath))
            {
                AppLogger.Info($"PresetPatchService: skipping missing file {filePath}");
                continue;
            }

            if (!IniParser.Apply(filePath, rules, backupFirst))
            {
                AppLogger.Warning($"PresetPatchService: Apply failed for {filePath}");
                allOk = false;
            }
        }
        return allOk;
    }

    private Dictionary<string, List<PresetPatchRule>> GroupRulesByFile(PatrolSetupPreset preset)
    {
        var map = new Dictionary<string, List<PresetPatchRule>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in preset.Rules)
        {
            var absPath = Path.Combine(_gtaPath, rule.File.Replace('/', Path.DirectorySeparatorChar));
            if (!map.TryGetValue(absPath, out var list))
            {
                list = [];
                map[absPath] = list;
            }
            list.Add(rule);
        }
        return map;
    }
}
