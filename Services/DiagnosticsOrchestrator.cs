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
        var rows = string.Join("\n", findings.Select(f =>
            $"<tr><td>{f.Severity}</td><td>{f.Category}</td><td>{f.Title}</td><td>{f.Detail}</td><td>{f.RecommendedFix}</td></tr>"));
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
