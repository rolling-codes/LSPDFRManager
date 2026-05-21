using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class CrashLogAnalyzerTests : CommandCenterTestBase
{
    [Fact]
    public void DetectsFatalKeyword()
    {
        var logPath = Path.Combine(TempDir, "RPH.log");
        File.WriteAllLines(logPath, ["[2024-01-01 12:00] FATAL: plugin crash detected"]);

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);

        Assert.Contains(findings, f => f.Severity == CrashLogSeverity.Fatal);
    }

    [Fact]
    public void DetectsCouldNotLoadKeyword()
    {
        var logPath = Path.Combine(TempDir, "test.log");
        File.WriteAllLines(logPath, ["[INFO] Could not load assembly MyMod.dll"]);

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);

        Assert.Contains(findings, f => f.SuspectedCause.Contains("DLL failed to load"));
    }

    [Fact]
    public void ReturnsEmpty_ForCleanLog()
    {
        var logPath = Path.Combine(TempDir, "clean.log");
        File.WriteAllLines(logPath, ["[INFO] Plugin loaded", "[INFO] All nominal"]);

        Assert.Empty(new CrashLogAnalyzer().AnalyzeFile(logPath));
    }

    [Fact]
    public async Task ExportsTxt()
    {
        var logPath = Path.Combine(TempDir, "crash.log");
        File.WriteAllLines(logPath, ["FATAL: hard crash"]);
        var outPath = Path.Combine(TempDir, "report.txt");

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);
        await new CrashLogAnalyzer().ExportReportAsync(findings, outPath);

        Assert.True(File.Exists(outPath));
        Assert.NotEmpty(await File.ReadAllTextAsync(outPath));
    }
}
