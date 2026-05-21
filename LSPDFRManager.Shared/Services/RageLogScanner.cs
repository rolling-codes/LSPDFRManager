using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Discovers RAGE Plugin Hook / ScriptHook log files, parses them into structured sessions,
/// and produces <see cref="RageLogFinding"/> items that explain what failed, which plugin was
/// likely involved, and what the user should do next.
/// </summary>
public sealed class RageLogScanner
{
    private static readonly string[] KnownLogs =
    [
        "RagePluginHook.log",
        "ScriptHookV.log",
        "ScriptHookVDotNet.log",
    ];

    private readonly RageLogParser _parser = new();

    /// <summary>
    /// Scans all known log files under the GTA V path and returns one session per file found.
    /// Missing files are silently skipped (not reported as findings).
    /// </summary>
    public IReadOnlyList<RageLogSession> ScanAll()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var sessions = new List<RageLogSession>();

        foreach (var name in KnownLogs)
        {
            var path = Path.Combine(gtaPath, name);
            var session = TryScanFile(path, name);
            if (session is not null)
                sessions.Add(session);
        }

        // Plugin-specific logs under plugins/LSPDFR/ and plugins/
        foreach (var subdir in new[] { "plugins/LSPDFR", "plugins" })
        {
            var dir = Path.Combine(gtaPath, subdir);
            if (!Directory.Exists(dir)) continue;
            foreach (var logFile in Directory.EnumerateFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
            {
                var session = TryScanFile(logFile, Path.GetRelativePath(gtaPath, logFile));
                if (session is not null)
                    sessions.Add(session);
            }
        }

        return sessions;
    }

    /// <summary>Scans a single file and returns null if the file cannot be read.</summary>
    public RageLogSession? ScanFile(string path)
    {
        return TryScanFile(path, Path.GetFileName(path));
    }

    private RageLogSession? TryScanFile(string path, string name)
    {
        if (!File.Exists(path)) return null;
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (Exception ex)
        {
            AppLogger.Warning($"[RageLogScanner] Could not read {name}: {ex.Message}");
            return null;
        }

        var session = _parser.Parse(name, lines);
        var findings = ProduceFindings(session);
        return session with { Findings = findings };
    }

    private static IReadOnlyList<RageLogFinding> ProduceFindings(RageLogSession session)
    {
        var findings = new List<RageLogFinding>();
        string? lastPlugin = null;
        RageLogEntry? lastPluginEntry = null;

        // Walk entries in order, tracking plugin context
        foreach (var entry in session.Entries)
        {
            var lower = entry.Message.ToLowerInvariant();
            var allText = string.Join("\n", new[] { entry.Message }.Concat(entry.ContinuationLines));
            var allLower = allText.ToLowerInvariant();

            // Track plugin lifecycle context
            var pluginName = ExtractPluginName(entry.Message);
            if (pluginName is not null &&
                (lower.Contains("loading plugin") || lower.Contains("loaded plugin") ||
                 lower.Contains("initializing plugin") || lower.Contains("plugin started")))
            {
                lastPlugin = pluginName;
                lastPluginEntry = entry;
            }

            // Bad image format must be checked before missing-dependency because a BadImageFormatException
            // continuation can also contain "could not load file or assembly".
            if (allLower.Contains("badimageformatexception") || allLower.Contains("bad image format"))
            {
                findings.Add(new RageLogFinding(
                    Code: "bad-image-format",
                    Severity: CrashLogSeverity.Error,
                    Confidence: FindingConfidence.High,
                    Title: "Bad image format — 32/64-bit mismatch or corrupt DLL",
                    Explanation: "A DLL was compiled for a different CPU architecture than GTA V (x64) or is corrupt.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: ExtractFilePath(allText),
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Re-download the affected mod — the archive may be corrupt.", "Ensure the mod targets x64 and is compatible with your GTA V version."]
                ));
                continue;
            }

            // Missing dependency / assembly load failure
            if (allLower.Contains("could not load file or assembly") ||
                allLower.Contains("filenotfoundexception") ||
                allLower.Contains("assembly load failed"))
            {
                var dep = ExtractAssemblyName(allText);
                findings.Add(new RageLogFinding(
                    Code: "missing-dependency",
                    Severity: CrashLogSeverity.Error,
                    Confidence: FindingConfidence.High,
                    Title: dep is not null ? $"Missing dependency: {dep}" : "Missing dependency or assembly",
                    Explanation: "A required DLL or assembly could not be found. The plugin that depends on it will not function.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: dep,
                    MissingDependency: dep,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Re-install the plugin that requires this dependency.", "Ensure the required framework or supporting DLL is present in the GTA V folder or plugins folder."]
                ));
                continue;
            }

            // Version mismatch
            if (allLower.Contains("version mismatch") || allLower.Contains("incompatible") ||
                allLower.Contains("typeloa­dexception") || allLower.Contains("missingmethodexception"))
            {
                findings.Add(new RageLogFinding(
                    Code: "version-mismatch",
                    Severity: CrashLogSeverity.Error,
                    Confidence: FindingConfidence.Medium,
                    Title: "Version mismatch or incompatible plugin",
                    Explanation: "A plugin or dependency was built against a different version of GTA V, RPH, or another shared library.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Update the mod to its latest version.", "Check if your GTA V version is supported by this plugin."]
                ));
                continue;
            }

            // Plugin aborted / crashed — match "aborted" alone since RPH logs format as
            // "Plugin 'Name.dll' aborted!" (not "plugin aborted" as a contiguous phrase)
            if (lower.Contains("aborted") || lower.Contains("plugin crashed") ||
                lower.Contains("could not load plugin"))
            {
                var plugin = pluginName ?? ExtractPluginName(entry.Message) ?? lastPlugin;
                findings.Add(new RageLogFinding(
                    Code: "plugin-load-failed",
                    Severity: CrashLogSeverity.Error,
                    Confidence: plugin is not null ? FindingConfidence.High : FindingConfidence.Medium,
                    Title: plugin is not null ? $"Plugin failed: {plugin}" : "Plugin failed to load or was aborted",
                    Explanation: "A plugin could not be loaded or was aborted during initialization.",
                    AffectedPlugin: plugin,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Check the log for the error that preceded this line.", "Disable the plugin and re-enable it after verifying dependencies."]
                ));
                continue;
            }

            // RPH hook failure
            if (lower.Contains("rph hook failed") || lower.Contains("failed to hook gta") ||
                lower.Contains("hook failed"))
            {
                findings.Add(new RageLogFinding(
                    Code: "rph-hook-failed",
                    Severity: CrashLogSeverity.Fatal,
                    Confidence: FindingConfidence.High,
                    Title: "RAGE Plugin Hook failed to hook GTA V",
                    Explanation: "RPH could not attach to GTA V. This usually means a GTA V update broke RPH compatibility.",
                    AffectedPlugin: null,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Update RAGE Plugin Hook to the latest version.", "Check lcpdfr.com for a compatibility update."]
                ));
                continue;
            }

            // ScriptHookV failure
            if (session.SourceLog.Contains("ScriptHookV", StringComparison.OrdinalIgnoreCase) &&
                (entry.Severity is CrashLogSeverity.Error or CrashLogSeverity.Fatal ||
                 lower.Contains("error") || lower.Contains("failed")))
            {
                findings.Add(new RageLogFinding(
                    Code: "script-hook-failed",
                    Severity: CrashLogSeverity.Error,
                    Confidence: FindingConfidence.Medium,
                    Title: "ScriptHookV error",
                    Explanation: "ScriptHookV reported an error. This may indicate a GTA V version mismatch.",
                    AffectedPlugin: null,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Update ScriptHookV to the latest version from Alexander Blade's site.", "Check if GTA V was updated recently."]
                ));
                continue;
            }

            // Unhandled exception block
            if (allLower.Contains("unhandled exception") ||
                (entry.ContinuationLines.Count > 2 &&
                 entry.ContinuationLines.Any(l => l.TrimStart().StartsWith("at ", StringComparison.Ordinal))))
            {
                findings.Add(new RageLogFinding(
                    Code: "unhandled-plugin-exception",
                    Severity: CrashLogSeverity.Error,
                    Confidence: FindingConfidence.Medium,
                    Title: "Unhandled plugin exception",
                    Explanation: "A plugin threw an unhandled exception. The stack trace may identify the failing plugin or method.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Report the stack trace to the plugin author.", "Disable the most recently added plugin and retest."]
                ));
                continue;
            }

            // Fatal / crash signal
            if (entry.Severity == CrashLogSeverity.Fatal ||
                lower.Contains("fatal") || lower.Contains("crash") ||
                lower.Contains("game terminated") || lower.Contains("process exited"))
            {
                findings.Add(new RageLogFinding(
                    Code: "crash-signal",
                    Severity: CrashLogSeverity.Fatal,
                    Confidence: FindingConfidence.Medium,
                    Title: "Crash or fatal error signal",
                    Explanation: lastPlugin is not null
                        ? $"A crash was detected. The last plugin context was '{lastPlugin}'."
                        : "A crash or fatal error was detected.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: BuildEvidence(entry),
                    SuggestedFixes: ["Review the log lines immediately before this entry.", "Disable the last-loaded plugin and retest."]
                ));
            }
        }

        // Emit last-plugin-before-crash when we have a last plugin and any fatal finding
        if (lastPlugin is not null && findings.Any(f => f.Severity == CrashLogSeverity.Fatal))
        {
            if (!findings.Any(f => f.Code == "last-plugin-before-crash"))
            {
                findings.Add(new RageLogFinding(
                    Code: "last-plugin-before-crash",
                    Severity: CrashLogSeverity.Warning,
                    Confidence: FindingConfidence.Low,
                    Title: $"Last plugin context before crash: {lastPlugin}",
                    Explanation: "This plugin was the most recently loaded/started before a crash was detected. It may or may not be responsible.",
                    AffectedPlugin: lastPlugin,
                    AffectedFile: null,
                    MissingDependency: null,
                    EvidenceLines: lastPluginEntry is not null ? BuildEvidence(lastPluginEntry) : [],
                    SuggestedFixes: ["Disable this plugin and retest to confirm if it is the cause."]
                ));
            }
        }

        return Deduplicate(findings);
    }

    private static IReadOnlyList<string> BuildEvidence(RageLogEntry entry)
    {
        var lines = new List<string> { entry.RawLine };
        lines.AddRange(entry.ContinuationLines.Take(10));
        return lines.AsReadOnly();
    }

    private static IReadOnlyList<RageLogFinding> Deduplicate(List<RageLogFinding> findings)
    {
        var seen = new HashSet<string>();
        var result = new List<RageLogFinding>();
        foreach (var f in findings)
        {
            var key = $"{f.Code}|{f.AffectedPlugin}|{f.MissingDependency}";
            if (seen.Add(key))
                result.Add(f);
        }
        return result.AsReadOnly();
    }

    private static string? ExtractPluginName(string message)
    {
        // Look for 'PluginName.dll' or quoted plugin names
        var m = System.Text.RegularExpressions.Regex.Match(
            message, @"['""]?([A-Za-z0-9_.]+\.dll)['""]?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractAssemblyName(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            text, "assembly '([^']+)'|assembly \"([^\"]+)\"|'([A-Za-z0-9_.]+\\.dll)'");
        if (!m.Success) return null;
        return (m.Groups[1].Value.Length > 0 ? m.Groups[1].Value :
                m.Groups[2].Value.Length > 0 ? m.Groups[2].Value :
                m.Groups[3].Value).Trim();
    }

    private static string? ExtractFilePath(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            text, @"[A-Za-z]:\\[^\n\r""']+\.(dll|exe|asi)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }
}
