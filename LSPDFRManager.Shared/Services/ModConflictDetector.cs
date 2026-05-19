using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModConflictDetector
{
    private static readonly (string Group, string[] Keywords, ConflictSeverity Severity, string Reason)[] ConflictGroups =
    [
        ("Dispatch/Police AI",  ["dispatch", "policeai", "aipolice", "police_ai"],                              ConflictSeverity.High,   "Multiple dispatch/police AI mods may conflict."),
        ("Traffic Density",     ["traffic", "trafficdensity", "population"],                                    ConflictSeverity.Medium, "Multiple traffic mods may conflict."),
        ("Handling",            ["handling"],                                                                    ConflictSeverity.Medium, "Multiple handling edits may conflict."),
        ("Gameconfig",          ["gameconfig"],                                                                  ConflictSeverity.High,   "Only one gameconfig.xml can be active."),
        ("Visual Settings",     ["visualsettings"],                                                              ConflictSeverity.Medium, "Multiple visual settings edits may conflict."),
        ("Callouts",            ["callout"],                                                                     ConflictSeverity.Low,    "Multiple callout packs are generally compatible."),
        ("EUP/Clothing",        ["eup", "clothing", "ped_"],                                                    ConflictSeverity.Low,    "Multiple EUP packs may conflict."),
        ("Sound/Sirens",        ["siren", "sound", "audio"],                                                    ConflictSeverity.Medium, "Multiple siren/audio mods may conflict."),
        ("Heap/Packfile",       ["heapadjuster", "packfilelimit"],                                              ConflictSeverity.High,   "Only one heap/packfile adjuster should be active."),
        ("ScriptHookV DotNet",  ["scripthookvdotnet", "shvdn"],                                                ConflictSeverity.Critical,"Multiple SHVDN versions will conflict."),
    ];

    public List<ModConflictResult> Detect()
    {
        var results = new List<ModConflictResult>();
        var gtaPath = AppConfig.Instance.GtaPath;
        if (!Directory.Exists(gtaPath)) return results;

        var allFiles = GetRelevantFiles(gtaPath).ToList();

        foreach (var (group, keywords, severity, reason) in ConflictGroups)
        {
            var matches = allFiles
                .Where(f => keywords.Any(kw => Path.GetFileName(f).Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count > 1)
            {
                results.Add(new ModConflictResult
                {
                    ConflictGroup = group,
                    InvolvedFiles = matches,
                    Reason = reason,
                    Severity = severity,
                    SuggestedFix = "Disable all but one.",
                    SafeRecommendation = "Test with Safe Launch Mode to isolate.",
                });
            }
        }

        // Gameconfig.xml specifics — only one allowed
        var gameConfigs = allFiles.Where(f => Path.GetFileName(f).Equals("gameconfig.xml", StringComparison.OrdinalIgnoreCase)).ToList();
        if (gameConfigs.Count > 1)
            results.Add(new ModConflictResult
            {
                ConflictGroup = "Multiple gameconfig.xml",
                InvolvedFiles = gameConfigs,
                Reason = "Only one gameconfig.xml can be active.",
                Severity = ConflictSeverity.Critical,
                SuggestedFix = "Keep only the one from your installed mods pack.",
            });

        return results;
    }

    private static IEnumerable<string> GetRelevantFiles(string gtaPath)
    {
        var dirs = new[] { "", "mods", "plugins", "plugins/lspdfr", "scripts", "x64" };
        foreach (var dir in dirs)
        {
            var full = Path.Combine(gtaPath, dir);
            if (!Directory.Exists(full)) continue;
            foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.TopDirectoryOnly))
                yield return file;
        }
    }
}
