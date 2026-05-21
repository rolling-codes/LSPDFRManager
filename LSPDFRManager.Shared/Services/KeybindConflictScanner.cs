using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class KeybindConflictScanner
{
    public List<KeybindConflict> Scan(IReadOnlyList<DiscoveredConfig> configs)
    {
        var iniConfigs = configs.Where(c => c.FileType.Equals("ini", StringComparison.OrdinalIgnoreCase)).ToList();

        // key: normalized value → list of ConflictEntry
        var valueMap = new Dictionary<string, List<ConflictEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var config in iniConfigs)
        {
            var parsed = IniParser.Parse(config.AbsolutePath);
            foreach (var (section, keys) in parsed)
            {
                foreach (var (key, value) in keys)
                {
                    if (!IsKeyLike(value))
                        continue;

                    if (!valueMap.TryGetValue(value, out var entries))
                    {
                        entries = [];
                        valueMap[value] = entries;
                    }

                    entries.Add(new ConflictEntry
                    {
                        FilePath = config.AbsolutePath,
                        Section = section,
                        Key = key,
                        PluginOwner = config.PluginOwner,
                    });
                }
            }
        }

        var conflicts = new List<KeybindConflict>();

        foreach (var (keyValue, entries) in valueMap)
        {
            var distinctFiles = entries.Select(e => e.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctFiles.Count < 2)
                continue;

            var severity = entries.Count >= 3 || distinctFiles.Count >= 3
                ? DiagnosticSeverity.Error
                : DiagnosticSeverity.Warning;

            conflicts.Add(new KeybindConflict
            {
                KeyValue = keyValue,
                Conflicts = [.. entries],
                Severity = severity,
            });
        }

        return conflicts;
    }

    private static bool IsKeyLike(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Equals("None", StringComparison.OrdinalIgnoreCase)) return false;
        if (value.Equals("0", StringComparison.OrdinalIgnoreCase)) return false;
        if (long.TryParse(value, out _)) return false;
        return true;
    }
}
