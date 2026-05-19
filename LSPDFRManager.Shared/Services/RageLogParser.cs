using System.Text.RegularExpressions;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Parses a RAGE Plugin Hook / ScriptHook log file into a structured <see cref="RageLogSession"/>.
/// Handles timestamped entries, severity/component tags, multi-line stack traces, and session
/// header metadata. Preserves every raw line so nothing is silently discarded.
/// </summary>
public sealed class RageLogParser
{
    // [MM/DD/YYYY HH:mm:ss.fff] or [YYYY-MM-DD HH:mm:ss.fff] followed by optional [SEVERITY] [COMPONENT]
    private static readonly Regex EntryPattern = new(
        @"^\[(?<ts>[^\]]+)\](?:\s+\[(?<sev>[A-Z]+)\])?(?:\s+\[(?<comp>[^\]]+)\])?\s*(?<msg>.*)",
        RegexOptions.Compiled);

    private static readonly Regex MetaPattern = new(
        @"^(?<key>RAGE Plugin Hook version|GTA V version|OS|Runtime|Started at|Launch arguments|Loaded path)[:\s]+(?<val>.+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RageLogSession Parse(string sourceName, IReadOnlyList<string> lines)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<RageLogEntry>();
        RageLogEntry? current = null;
        var continuations = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];

            // Try structured entry first
            var m = EntryPattern.Match(raw);
            if (m.Success)
            {
                CommitCurrent(ref current, continuations, entries);
                continuations = [];

                var ts = TryParseTimestamp(m.Groups["ts"].Value);
                var sev = ParseSeverity(m.Groups["sev"].Value);
                var comp = m.Groups["comp"].Success ? m.Groups["comp"].Value.Trim() : null;
                var msg = m.Groups["msg"].Value.Trim();

                current = new RageLogEntry(sourceName, i + 1, ts, sev, comp, msg, raw, []);

                // Collect session metadata from early lines (first 30)
                if (i < 30)
                {
                    var meta = MetaPattern.Match(raw.Trim());
                    if (!meta.Success) meta = MetaPattern.Match(msg);
                    if (meta.Success)
                        metadata[meta.Groups["key"].Value] = meta.Groups["val"].Value.Trim();
                }

                continue;
            }

            // Non-timestamped line — treat as continuation (stack trace, inner exception, etc.)
            if (current is not null)
            {
                continuations.Add(raw);
            }
            else
            {
                // Header / pre-first-entry metadata
                var meta = MetaPattern.Match(raw.Trim());
                if (meta.Success)
                    metadata[meta.Groups["key"].Value] = meta.Groups["val"].Value.Trim();

                entries.Add(new RageLogEntry(sourceName, i + 1, null, CrashLogSeverity.Info,
                    null, raw.Trim(), raw, []));
            }
        }

        CommitCurrent(ref current, continuations, entries);

        DateTimeOffset? startedAt = entries.FirstOrDefault(e => e.Timestamp.HasValue)?.Timestamp;

        return new RageLogSession(sourceName, startedAt, metadata, entries, []);
    }

    private static void CommitCurrent(ref RageLogEntry? current, List<string> continuations, List<RageLogEntry> entries)
    {
        if (current is null) return;
        entries.Add(current with { ContinuationLines = continuations.AsReadOnly() });
        current = null;
    }

    private static DateTimeOffset? TryParseTimestamp(string raw)
    {
        var formats = new[]
        {
            "MM/dd/yyyy HH:mm:ss.fff",
            "MM/dd/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
        };
        foreach (var fmt in formats)
            if (DateTimeOffset.TryParseExact(raw.Trim(), fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out var result))
                return result;
        return null;
    }

    private static CrashLogSeverity ParseSeverity(string tag) => tag.ToUpperInvariant() switch
    {
        "FATAL" or "CRITICAL" => CrashLogSeverity.Fatal,
        "ERROR" or "ERR"      => CrashLogSeverity.Error,
        "WARN" or "WARNING"   => CrashLogSeverity.Warning,
        _                     => CrashLogSeverity.Info,
    };
}
