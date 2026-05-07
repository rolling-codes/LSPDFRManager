using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class CrashLogAnalyzer
{
    private static readonly (string Keyword, string Cause, string Fix, CrashLogSeverity Severity)[] Patterns =
    [
        ("fatal",                   "Fatal error in plugin or engine",          "Check which plugin was last loaded.",          CrashLogSeverity.Fatal),
        ("crash",                   "Application crash detected",               "Review full log for stack trace.",             CrashLogSeverity.Fatal),
        ("stack overflow",          "Stack overflow — likely infinite loop",     "Disable recently installed script mods.",      CrashLogSeverity.Fatal),
        ("out of memory",           "Out of memory",                            "Try gameconfig/heap adjuster mods.",           CrashLogSeverity.Fatal),
        ("access denied",           "File access denied",                       "Run as administrator or check permissions.",   CrashLogSeverity.Error),
        ("unauthorized",            "Unauthorized access",                      "Run as administrator.",                        CrashLogSeverity.Error),
        ("could not load",          "Plugin or DLL failed to load",             "Check for missing dependencies.",              CrashLogSeverity.Error),
        ("bad image format",        "32/64-bit mismatch or corrupt DLL",        "Re-download the affected mod.",                CrashLogSeverity.Error),
        ("file not found",          "Required file missing",                    "Re-install the affected mod.",                 CrashLogSeverity.Error),
        ("assembly load",           "Assembly failed to load",                  "Check .NET runtime version compatibility.",    CrashLogSeverity.Error),
        ("incompatible",            "Version incompatibility detected",         "Update the mod or check GTA V version.",       CrashLogSeverity.Error),
        ("null reference",          "Null reference in plugin",                 "Report to plugin author.",                     CrashLogSeverity.Error),
        ("invalid operation",       "Invalid operation in plugin",              "Report to plugin author.",                     CrashLogSeverity.Error),
        ("plugin aborted",          "Plugin was aborted",                       "Check plugin logs for the specific error.",    CrashLogSeverity.Error),
        ("rph hook failed",         "RAGEPluginHook failed to hook GTA V",      "Verify GTA V version and RPH version match.",  CrashLogSeverity.Error),
        ("timeout",                 "Operation timed out",                      "Disable network-dependent mods.",              CrashLogSeverity.Warning),
        ("failed",                  "Generic failure detected",                 "Check surrounding log lines for context.",     CrashLogSeverity.Warning),
        ("missing",                 "Missing resource or file",                 "Re-install the affected mod.",                 CrashLogSeverity.Warning),
        ("exception",               "Exception in plugin or script",            "Check plugin log for details.",                CrashLogSeverity.Warning),
    ];

    private static readonly string[] DefaultLogFiles =
    [
        "RagePluginHook.log",
        "ScriptHookV.log",
        "ScriptHookVDotNet.log",
    ];

    public List<CrashLogFinding> AnalyzeAll()
    {
        var results = new List<CrashLogFinding>();
        var gtaPath = AppConfig.Instance.GtaPath;

        foreach (var logName in DefaultLogFiles)
        {
            var logPath = Path.Combine(gtaPath, logName);
            if (File.Exists(logPath))
                results.AddRange(AnalyzeFile(logPath));
        }

        var managerLog = AppDataPaths.LogFile;
        if (File.Exists(managerLog))
            results.AddRange(AnalyzeFile(managerLog));

        return results;
    }

    public List<CrashLogFinding> AnalyzeFile(string logPath)
    {
        var results = new List<CrashLogFinding>();

        if (!File.Exists(logPath))
            return results;

        string[] lines;
        try { lines = File.ReadAllLines(logPath); }
        catch { return results; }

        var logName = Path.GetFileName(logPath);

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            foreach (var (keyword, cause, fix, severity) in Patterns)
            {
                if (!lower.Contains(keyword)) continue;

                results.Add(new CrashLogFinding
                {
                    SourceLog = logName,
                    SuspectedCause = cause,
                    RecommendedFix = fix,
                    MatchedLine = line.Length > 200 ? line[..200] : line,
                    Severity = severity,
                });
                break;
            }
        }

        return results;
    }

    public async Task<string> ExportReportAsync(List<CrashLogFinding> findings, string outputPath)
    {
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        if (ext == ".json")
        {
            var json = System.Text.Json.JsonSerializer.Serialize(findings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }
        else if (ext == ".html")
        {
            var rows = string.Join("\n", findings.Select(f =>
                $"<tr><td>{f.Severity}</td><td>{f.SourceLog}</td><td>{f.SuspectedCause}</td><td>{f.RecommendedFix}</td><td>{System.Web.HttpUtility.HtmlEncode(f.MatchedLine)}</td></tr>"));
            var html = $"<html><body><h1>Crash Report</h1><table border='1'><tr><th>Severity</th><th>Source</th><th>Cause</th><th>Fix</th><th>Line</th></tr>{rows}</table></body></html>";
            await File.WriteAllTextAsync(outputPath, html);
        }
        else
        {
            var lines = findings.Select(f => $"[{f.Severity}] [{f.SourceLog}] {f.SuspectedCause} — {f.RecommendedFix}\n  {f.MatchedLine}");
            await File.WriteAllLinesAsync(outputPath, lines);
        }

        return outputPath;
    }
}
