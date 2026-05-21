using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public static class IniParser
{
    public static Dictionary<string, Dictionary<string, string>> Parse(string filePath)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines = File.ReadAllLines(filePath);
            var currentSection = "";
            result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed[1..^1].Trim();
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                if (trimmed.StartsWith(';') || trimmed.StartsWith('#') || !trimmed.Contains('='))
                    continue;

                var eqIdx = trimmed.IndexOf('=');
                var key = trimmed[..eqIdx].Trim();
                var value = trimmed[(eqIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                    result[currentSection][key] = value;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"IniParser.Parse failed for {filePath}: {ex.Message}");
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    public static List<IniPatchPreview> PreviewPatch(string filePath, IEnumerable<PresetPatchRule> rules)
    {
        var previews = new List<IniPatchPreview>();
        var ruleList = rules.ToList();
        if (ruleList.Count == 0) return previews;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"IniParser.PreviewPatch: cannot read {filePath}: {ex.Message}");
            return previews;
        }

        var currentSection = "";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                continue;
            }

            if (!trimmed.Contains('=') || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            var eqIdx = trimmed.IndexOf('=');
            var key = trimmed[..eqIdx].Trim();
            var currentValue = trimmed[(eqIdx + 1)..].Trim();

            foreach (var rule in ruleList)
            {
                if (!rule.MatchKeys.Any(mk => mk.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                previews.Add(new IniPatchPreview
                {
                    FilePath = filePath,
                    Section = currentSection,
                    Key = key,
                    OldValue = currentValue,
                    NewValue = rule.SetValue,
                    Reason = rule.Reason,
                    WouldChange = !currentValue.Equals(rule.SetValue, StringComparison.Ordinal),
                });
            }
        }

        return previews.Where(p => p.WouldChange).ToList();
    }

    public static bool Apply(string filePath, IEnumerable<PresetPatchRule> rules, bool backupFirst = true)
    {
        var ruleList = rules.ToList();
        if (ruleList.Count == 0) return true;

        string[] lines;
        string newline;
        try
        {
            var raw = File.ReadAllText(filePath);
            newline = raw.Contains("\r\n") ? "\r\n" : "\n";
            lines = [.. raw.Split('\n').Select(l => l.TrimEnd('\r'))];
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"IniParser.Apply: cannot read {filePath}: {ex.Message}");
            return false;
        }

        var currentSection = "";
        var outputLines = new string[lines.Length];
        bool anyChange = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                outputLines[i] = line;
                continue;
            }

            if (!trimmed.Contains('=') || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                outputLines[i] = line;
                continue;
            }

            var eqIdx = trimmed.IndexOf('=');
            var key = trimmed[..eqIdx].Trim();
            var matched = ruleList.FirstOrDefault(r =>
                r.MatchKeys.Any(mk => mk.Equals(key, StringComparison.OrdinalIgnoreCase)));

            if (matched == null)
            {
                outputLines[i] = line;
                continue;
            }

            var rawEqIdx = line.IndexOf('=');
            var beforeEq = line[..(rawEqIdx + 1)];
            // Preserve leading space on value side if present
            var afterEq = line[(rawEqIdx + 1)..];
            var leadingSpace = afterEq.Length > 0 && afterEq[0] == ' ' ? " " : "";
            outputLines[i] = beforeEq + leadingSpace + matched.SetValue;

            if (outputLines[i] != line)
                anyChange = true;
        }

        if (!anyChange)
            return true;

        try
        {
            if (backupFirst)
            {
                var bakPath = filePath + ".bak";
                var bakInfo = File.Exists(bakPath) ? new FileInfo(bakPath) : null;
                var srcInfo = new FileInfo(filePath);
                if (bakInfo == null)
                    File.Copy(filePath, bakPath, overwrite: false);
            }

            File.WriteAllText(filePath, string.Join(newline, outputLines) + newline);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"IniParser.Apply: write failed for {filePath}: {ex.Message}");
            return false;
        }
    }
}
