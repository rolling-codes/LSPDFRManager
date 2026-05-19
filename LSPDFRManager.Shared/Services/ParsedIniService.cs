using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Reads a .ini file into typed <see cref="IniConfigEntry"/> records and writes individual
/// key edits back while preserving all comments and unrelated formatting.
/// </summary>
public static class ParsedIniService
{
    private static readonly HashSet<string> KeybindKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "key", "hotkey", "keybind", "shortcut", "button" };

    private static readonly HashSet<string> PathKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "path", "folder", "directory", "file", "dir", "location" };

    private static readonly HashSet<string> ColorKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "color", "colour", "rgb", "hex" };

    public static List<IniConfigEntry> Parse(string filePath)
    {
        var entries = new List<IniConfigEntry>();
        try
        {
            var lines = File.ReadAllLines(filePath);
            var currentSection = "";
            string? pendingComment = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed[1..^1].Trim();
                    pendingComment = null;
                    continue;
                }

                if (trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                {
                    pendingComment = trimmed.TrimStart(';', '#').Trim();
                    continue;
                }

                if (!trimmed.Contains('='))
                {
                    pendingComment = null;
                    continue;
                }

                var eqIdx = trimmed.IndexOf('=');
                var key = trimmed[..eqIdx].Trim();
                var value = trimmed[(eqIdx + 1)..].Trim();

                // Strip inline comment from value
                var commentIdx = value.IndexOf(';');
                string? inlineComment = null;
                if (commentIdx > 0)
                {
                    inlineComment = value[(commentIdx + 1)..].Trim();
                    value = value[..commentIdx].Trim();
                }

                if (string.IsNullOrEmpty(key)) continue;

                var comment = pendingComment ?? inlineComment;
                var type = InferType(key, value);

                entries.Add(new IniConfigEntry
                {
                    FilePath     = filePath,
                    Section      = currentSection,
                    Key          = key,
                    RawValue     = value,
                    EditValue    = value,
                    InferredType = type,
                    Comment      = comment,
                });

                pendingComment = null;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"ParsedIniService.Parse failed for '{filePath}': {ex.Message}");
        }

        return entries;
    }

    /// <summary>
    /// Writes the edited value for a single key back to the file, preserving all other content.
    /// Creates a .bak backup before modifying.
    /// </summary>
    public static bool SaveEntry(IniConfigEntry entry)
    {
        try
        {
            var rule = new PresetPatchRule
            {
                MatchKeys = [entry.Key],
                SetValue  = entry.EditValue,
                Reason    = $"User edited {entry.Key} in config editor.",
            };
            return IniParser.Apply(entry.FilePath, [rule], backupFirst: true);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"ParsedIniService.SaveEntry failed for '{entry.FilePath}' key '{entry.Key}': {ex.Message}");
            return false;
        }
    }

    private static IniValueType InferType(string key, string value)
    {
        var lk = key.ToLowerInvariant();
        var lv = value.ToLowerInvariant();

        foreach (var kw in ColorKeywords)
            if (lk.Contains(kw)) return IniValueType.Color;

        foreach (var kw in PathKeywords)
            if (lk.Contains(kw)) return IniValueType.Path;

        foreach (var kw in KeybindKeywords)
            if (lk.Contains(kw)) return IniValueType.Keybind;

        if (lv is "true" or "false" or "yes" or "no" or "1" or "0")
            return IniValueType.Bool;

        if (int.TryParse(value, out _)) return IniValueType.Int;
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return IniValueType.Float;

        // Hex color: #RRGGBB or RRGGBB
        if (value.StartsWith('#') && (value.Length == 7 || value.Length == 9))
            return IniValueType.Color;

        return IniValueType.String;
    }
}
