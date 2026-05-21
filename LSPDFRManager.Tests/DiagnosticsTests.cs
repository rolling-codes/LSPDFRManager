using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class DiagnosticsTests : CommandCenterTestBase
{
    [Fact]
    public void ConflictDetector_DetectsMultipleGameconfigs()
    {
        File.WriteAllText(Path.Combine(GtaDir, "gameconfig.xml"), "<GameConfig/>");
        var modsDir = Path.Combine(GtaDir, "mods");
        Directory.CreateDirectory(modsDir);
        File.WriteAllText(Path.Combine(modsDir, "gameconfig.xml"), "<GameConfig/>");

        var results = new ModConflictDetector().Detect();

        Assert.Contains(results, r => r.ConflictGroup.Contains("gameconfig", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConflictDetector_NoConflicts_WhenClean()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        Assert.Empty(new ModConflictDetector().Detect());
    }

    [Fact]
    public async Task Orchestrator_ExportTxt_CreatesFile()
    {
        var findings = new List<DiagnosticFinding>
        {
            new() { Category = "Test", Title = "A finding", Detail = "detail", Severity = DiagnosticSeverity.Warning }
        };
        var outPath = Path.Combine(TempDir, "report.txt");

        await new DiagnosticsOrchestrator().ExportReportAsync(findings, outPath);

        Assert.True(File.Exists(outPath));
        Assert.Contains("A finding", await File.ReadAllTextAsync(outPath));
    }

    [Fact]
    public async Task Orchestrator_ExportHtml_ContainsTable()
    {
        var findings = new List<DiagnosticFinding>
        {
            new() { Category = "Deps", Title = "Missing ScriptHookV", Severity = DiagnosticSeverity.Warning }
        };
        var outPath = Path.Combine(TempDir, "report.html");

        await new DiagnosticsOrchestrator().ExportReportAsync(findings, outPath);

        var content = await File.ReadAllTextAsync(outPath);
        Assert.Contains("<table", content);
        Assert.Contains("Missing ScriptHookV", content);
    }

    [Fact]
    public void StorageAnalyzer_ReportsPluginsFolder()
    {
        var dir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.dll"), "content");

        var results = new StorageUsageAnalyzer().Analyze();

        var plugins = results.FirstOrDefault(r => r.Label == "Plugins");
        Assert.NotNull(plugins);
        Assert.True(plugins!.SizeBytes > 0);
    }

    [Fact]
    public void StorageUsageResult_SizeDisplay_FormatsMB()
    {
        var r = new StorageUsageResult { SizeBytes = 2 * 1024 * 1024 };
        Assert.Contains("MB", r.SizeDisplay);
    }

    [Fact]
    public void StorageUsageResult_SizeDisplay_FormatsKB()
    {
        var r = new StorageUsageResult { SizeBytes = 500 };
        Assert.Contains("KB", r.SizeDisplay);
    }
}
