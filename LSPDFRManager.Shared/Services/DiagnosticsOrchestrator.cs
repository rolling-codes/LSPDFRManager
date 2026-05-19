using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class DiagnosticsOrchestrator
{
    private readonly PluginHealthScanner _pluginScanner = new();
    private readonly DependencyScanner _depScanner = new();
    private readonly ModConflictDetector _conflictDetector = new();
    private readonly StorageUsageAnalyzer _storageAnalyzer = new();

    public async Task<List<DiagnosticFinding>> RunAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var findings = new List<DiagnosticFinding>();

        progress?.Report("Scanning plugins…");
        var pluginResults = await Task.Run(() => _pluginScanner.Scan(), ct);
        findings.AddRange(pluginResults.Select(r => new DiagnosticFinding
        {
            Category = "Plugin Health",
            Title = r.Issue,
            Detail = r.FilePath,
            RecommendedFix = r.RecommendedFix,
            AffectedPath = r.FilePath,
            Severity = r.Severity switch
            {
                PluginScanSeverity.Ok => DiagnosticSeverity.Ok,
                PluginScanSeverity.Info => DiagnosticSeverity.Info,
                PluginScanSeverity.Warning => DiagnosticSeverity.Warning,
                _ => DiagnosticSeverity.Error,
            }
        }));

        ct.ThrowIfCancellationRequested();
        progress?.Report("Scanning dependencies…");
        var depResults = await Task.Run(() => _depScanner.Scan(), ct);
        findings.AddRange(depResults.Where(r => r.Status is DependencyStatus.Missing or DependencyStatus.WrongLocation).Select(r => new DiagnosticFinding
        {
            Category = "Dependencies",
            Title = $"{r.Name}: {r.Status}",
            Detail = r.Note ?? $"Expected at: {r.ExpectedPath}",
            AffectedPath = r.ExpectedPath,
            Severity = r.Status == DependencyStatus.Missing ? DiagnosticSeverity.Warning : DiagnosticSeverity.Info,
        }));

        ct.ThrowIfCancellationRequested();
        progress?.Report("Detecting conflicts…");
        var conflicts = await Task.Run(() => _conflictDetector.Detect(), ct);
        findings.AddRange(conflicts.Select(c => new DiagnosticFinding
        {
            Category = "Conflicts",
            Title = c.ConflictGroup,
            Detail = c.Reason,
            RecommendedFix = c.SuggestedFix,
            Severity = c.Severity switch
            {
                ConflictSeverity.Low => DiagnosticSeverity.Info,
                ConflictSeverity.Medium => DiagnosticSeverity.Warning,
                ConflictSeverity.High => DiagnosticSeverity.Error,
                _ => DiagnosticSeverity.Critical,
            }
        }));

        ct.ThrowIfCancellationRequested();
        progress?.Report("Analyzing storage…");
        var storageResults = await Task.Run(() => _storageAnalyzer.Analyze(), ct);
        foreach (var s in storageResults.Where(r => r.SizeBytes > 512L * 1024 * 1024))
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Storage",
                Title = $"{s.Label} is large ({s.SizeDisplay})",
                Detail = s.FolderPath,
                Severity = DiagnosticSeverity.Info,
            });
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report("Running Setup Doctor…");
        var doctorFindings = await Task.Run(async () => await new SetupDoctorService().RunAsync(ct), ct);
        findings.AddRange(doctorFindings.Where(f => f.Severity != DiagnosticSeverity.Ok));

        ct.ThrowIfCancellationRequested();
        progress?.Report("Scanning RAGE/RPH logs…");
        var rageSessions = await Task.Run(() => new RageLogScanner().ScanAll(), ct);
        foreach (var session in rageSessions)
        {
            foreach (var rf in session.Findings)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Log Analysis",
                    Title = rf.Title,
                    Detail = rf.Explanation +
                             (rf.AffectedPlugin is not null ? $" Plugin: {rf.AffectedPlugin}." : "") +
                             (rf.MissingDependency is not null ? $" Missing: {rf.MissingDependency}." : ""),
                    RecommendedFix = string.Join(" ", rf.SuggestedFixes),
                    AffectedPath = session.SourceLog,
                    Severity = rf.Severity switch
                    {
                        CrashLogSeverity.Fatal   => DiagnosticSeverity.Critical,
                        CrashLogSeverity.Error   => DiagnosticSeverity.Error,
                        CrashLogSeverity.Warning => DiagnosticSeverity.Warning,
                        _                        => DiagnosticSeverity.Info,
                    },
                    Confidence = 1.0f,
                });
            }
        }

        // DLL duplicate scan
        ct.ThrowIfCancellationRequested();
        progress?.Report("Scanning for duplicate DLLs…");
        var dllDups = await Task.Run(() => new DllDuplicateScanner().Scan(), ct);
        foreach (var dup in dllDups)
        {
            var sev = dup.IsKnownSharedDep ? DiagnosticSeverity.Warning : DiagnosticSeverity.Info;
            findings.Add(new DiagnosticFinding
            {
                Category = "Dependencies",
                Title = $"Duplicate DLL: {dup.DllName} ({dup.Count} copies)",
                Detail = $"Found in: {string.Join(", ", dup.Copies)}",
                RecommendedFix = "Keep only one copy of this DLL. Plugin-bundled copies can cause version conflicts.",
                AffectedPath = dup.Copies.Count > 0 ? dup.Copies[0] : null,
                Severity = sev,
                Confidence = 1.0f,
            });
        }

        AppConfig.Instance.LastDiagnosticsScanUtc = DateTime.UtcNow;
        AppConfig.Instance.Save();

        progress?.Report($"Scan complete — {findings.Count} findings.");
        return findings;
    }

    public async Task<string> ExportReportAsync(List<DiagnosticFinding> findings, string outputPath)
    {
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        if (ext == ".html")
        {
            var html = BuildHtmlReport(findings);
            await File.WriteAllTextAsync(outputPath, html);
        }
        else if (ext == ".json")
        {
            var json = System.Text.Json.JsonSerializer.Serialize(findings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }
        else
        {
            var lines = findings.Select(f => $"[{f.Severity}] [{f.Category}] {f.Title}: {f.Detail}");
            await File.WriteAllLinesAsync(outputPath, lines);
        }

        return outputPath;
    }

    private static string BuildHtmlReport(List<DiagnosticFinding> findings)
    {
        Func<string, string> e = System.Net.WebUtility.HtmlEncode;
        var rows = string.Join("\n", findings.Select(f =>
            $"<tr><td>{e(f.Severity.ToString())}</td><td>{e(f.Category)}</td><td>{e(f.Title)}</td><td>{e(f.Detail ?? "")}</td><td>{e(f.RecommendedFix ?? "")}</td></tr>"));
        return $"""
            <html><head><title>Diagnostics Report</title></head><body>
            <h1>LSPDFR Manager Diagnostics Report</h1>
            <p>Generated: {DateTime.Now}</p>
            <table border="1"><tr><th>Severity</th><th>Category</th><th>Title</th><th>Detail</th><th>Fix</th></tr>
            {rows}
            </table></body></html>
            """;
    }
}
