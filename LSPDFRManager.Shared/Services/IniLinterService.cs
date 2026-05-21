using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Lints .ini, .xml, and .json config files for common authoring errors.
/// Returns a list of <see cref="LintFinding"/> — never throws on malformed input.
/// </summary>
public sealed class IniLinterService
{
    private static readonly string[] SupportedExtensions = [".ini", ".cfg", ".xml", ".json", ".meta"];

    public bool Supports(string filePath) =>
        SupportedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LintFinding> Lint(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".ini" or ".cfg" => LintIni(filePath),
            ".xml" or ".meta" => LintXml(filePath),
            ".json" => LintJson(filePath),
            _ => [],
        };
    }

    // ── INI linter ────────────────────────────────────────────────────────────

    private static IReadOnlyList<LintFinding> LintIni(string filePath)
    {
        var findings = new List<LintFinding>();
        string[] lines;
        try { lines = File.ReadAllLines(filePath); }
        catch (Exception ex)
        {
            return [new LintFinding(filePath, null, null, null, "unreadable", $"Cannot read file: {ex.Message}", DiagnosticSeverity.Error)];
        }

        var currentSection = "";
        // section → list of keys seen (lowercase) so we can detect duplicates
        var seen = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            var lineNo = i + 1;

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                if (!seen.ContainsKey(currentSection))
                    seen[currentSection] = [];
                continue;
            }

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                findings.Add(new LintFinding(filePath, lineNo, currentSection, null,
                    "malformed-line", "Line has no '=' separator and is not a comment or section header.",
                    DiagnosticSeverity.Warning));
                continue;
            }

            var key   = trimmed[..eqIdx].Trim();
            var value = trimmed[(eqIdx + 1)..].Trim();
            var keyLower = key.ToLowerInvariant();

            if (!seen.TryGetValue(currentSection, out var sectionKeys))
            {
                sectionKeys = [];
                seen[currentSection] = sectionKeys;
            }

            if (sectionKeys.Contains(keyLower))
            {
                findings.Add(new LintFinding(filePath, lineNo, currentSection, key,
                    "duplicate-key", $"Key '{key}' appears more than once in section '[{currentSection}]'.",
                    DiagnosticSeverity.Warning));
            }
            else
            {
                sectionKeys.Add(keyLower);
            }

            // Empty value for non-comment line
            if (string.IsNullOrEmpty(value))
            {
                findings.Add(new LintFinding(filePath, lineNo, currentSection, key,
                    "empty-value", $"Key '{key}' has an empty value.",
                    DiagnosticSeverity.Info));
            }

            // Bad boolean (truthy garbage)
            if (LooksLikeBoolean(key) && !IsValidBoolean(value))
            {
                findings.Add(new LintFinding(filePath, lineNo, currentSection, key,
                    "bad-boolean", $"Key '{key}' looks like a boolean but has value '{value}'. Expected true/false/1/0/yes/no.",
                    DiagnosticSeverity.Warning));
            }
        }

        return findings;
    }

    // ── XML linter ────────────────────────────────────────────────────────────

    private static IReadOnlyList<LintFinding> LintXml(string filePath)
    {
        var findings = new List<LintFinding>();
        string text;
        try { text = File.ReadAllText(filePath); }
        catch (Exception ex)
        {
            return [new LintFinding(filePath, null, null, null, "unreadable", $"Cannot read file: {ex.Message}", DiagnosticSeverity.Error)];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            findings.Add(new LintFinding(filePath, null, null, null, "empty-file", "File is empty.", DiagnosticSeverity.Warning));
            return findings;
        }

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(text, System.Xml.Linq.LoadOptions.SetLineInfo);
            _ = doc; // parsed OK
        }
        catch (System.Xml.XmlException ex)
        {
            findings.Add(new LintFinding(filePath, ex.LineNumber > 0 ? ex.LineNumber : null, null, null,
                "invalid-xml", $"XML parse error: {ex.Message}", DiagnosticSeverity.Error));
        }

        return findings;
    }

    // ── JSON linter ───────────────────────────────────────────────────────────

    private static IReadOnlyList<LintFinding> LintJson(string filePath)
    {
        var findings = new List<LintFinding>();
        string text;
        try { text = File.ReadAllText(filePath); }
        catch (Exception ex)
        {
            return [new LintFinding(filePath, null, null, null, "unreadable", $"Cannot read file: {ex.Message}", DiagnosticSeverity.Error)];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            findings.Add(new LintFinding(filePath, null, null, null, "empty-file", "File is empty.", DiagnosticSeverity.Warning));
            return findings;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            _ = doc.RootElement; // parsed OK
        }
        catch (System.Text.Json.JsonException ex)
        {
            findings.Add(new LintFinding(filePath, (int?)ex.LineNumber + 1, null, null,
                "invalid-json", $"JSON parse error: {ex.Message}", DiagnosticSeverity.Error));
        }

        return findings;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static readonly string[] BooleanKeywords =
        ["enable", "disable", "active", "visible", "show", "use", "auto", "toggle", "debug", "log"];

    private static bool LooksLikeBoolean(string key) =>
        BooleanKeywords.Any(kw => key.Contains(kw, StringComparison.OrdinalIgnoreCase));

    private static bool IsValidBoolean(string value) =>
        value is "true" or "false" or "1" or "0" or "yes" or "no"
               or "True" or "False" or "Yes" or "No" or "TRUE" or "FALSE";
}
