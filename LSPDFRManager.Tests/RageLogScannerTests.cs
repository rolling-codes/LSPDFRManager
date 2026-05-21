using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Fixture-based tests for <see cref="RageLogParser"/> and <see cref="RageLogScanner"/>.
/// All tests use fixture log files from Fixtures/Logs/ — no real GTA V install required.
/// </summary>
public class RageLogScannerTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "Logs", name);

    private static RageLogSession ParseFixture(string fileName)
    {
        var path = FixturePath(fileName);
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse(fileName, lines);
        var scanner = new RageLogScanner();
        var full = scanner.ScanFile(path);
        return full ?? session;
    }

    // ── Parser correctness ──────────────────────────────────────────────────

    [Fact]
    public void Parser_PreservesEveryRawLine()
    {
        var path = FixturePath("clean-rph-log.txt");
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse("clean-rph-log.txt", lines);

        // Every raw line must appear in exactly one entry (as RawLine or in ContinuationLines)
        var allRaw = session.Entries
            .SelectMany(e => new[] { e.RawLine }.Concat(e.ContinuationLines))
            .ToList();

        foreach (var line in lines)
            Assert.Contains(line, allRaw);
    }

    [Fact]
    public void Parser_GroupsStackTraceContinuations()
    {
        var path = FixturePath("multi-line-stacktrace-rph-log.txt");
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse("multi-line-stacktrace-rph-log.txt", lines);

        // The error entry should have continuation lines (stack trace)
        var errorEntry = session.Entries.FirstOrDefault(e =>
            e.Message.Contains("Exception in plugin", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(errorEntry);
        Assert.True(errorEntry.ContinuationLines.Count > 2,
            "Stack trace lines should be grouped as continuations");
    }

    [Fact]
    public void Parser_ParsesTimestamps()
    {
        var path = FixturePath("clean-rph-log.txt");
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse("clean-rph-log.txt", lines);

        Assert.True(session.Entries.Any(e => e.Timestamp.HasValue),
            "At least one entry should have a parsed timestamp");
    }

    [Fact]
    public void Parser_DoesNotCrashOnMalformedLog()
    {
        var path = FixturePath("malformed-or-partial-log.txt");
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse("malformed-or-partial-log.txt", lines);

        // Must not throw and must return entries
        Assert.NotNull(session);
        Assert.True(session.Entries.Count > 0);
    }

    [Fact]
    public void Parser_SetsSessionStartedAt_FromFirstTimestamp()
    {
        var path = FixturePath("clean-rph-log.txt");
        var lines = File.ReadAllLines(path);
        var session = new RageLogParser().Parse("clean-rph-log.txt", lines);

        Assert.NotNull(session.StartedAt);
    }

    // ── Finding detection ───────────────────────────────────────────────────

    [Fact]
    public void Scanner_CleanLog_ProducesNoFindings()
    {
        var session = ParseFixture("clean-rph-log.txt");

        Assert.Empty(session.Findings);
    }

    [Fact]
    public void Scanner_MissingDependency_DetectedWithHighConfidence()
    {
        var session = ParseFixture("missing-dependency-rph-log.txt");

        var finding = session.Findings.FirstOrDefault(f => f.Code == "missing-dependency");
        Assert.NotNull(finding);
        Assert.Equal(FindingConfidence.High, finding.Confidence);
        Assert.Equal(CrashLogSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Scanner_MissingDependency_ExtractsDependencyName()
    {
        var session = ParseFixture("missing-dependency-rph-log.txt");

        var finding = session.Findings.First(f => f.Code == "missing-dependency");
        Assert.NotNull(finding.MissingDependency);
        Assert.Contains("RAGENativeUI", finding.MissingDependency, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scanner_MissingDependency_IncludesEvidenceLines()
    {
        var session = ParseFixture("missing-dependency-rph-log.txt");

        var finding = session.Findings.First(f => f.Code == "missing-dependency");
        Assert.NotEmpty(finding.EvidenceLines);
    }

    [Fact]
    public void Scanner_MissingDependency_CapturesLastPluginContext()
    {
        var session = ParseFixture("missing-dependency-rph-log.txt");

        var finding = session.Findings.First(f => f.Code == "missing-dependency");
        Assert.NotNull(finding.AffectedPlugin);
        Assert.Contains("StopThePed", finding.AffectedPlugin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scanner_PluginAborted_DetectedAsPluginLoadFailed()
    {
        var session = ParseFixture("plugin-aborted-rph-log.txt");

        var finding = session.Findings.FirstOrDefault(f => f.Code == "plugin-load-failed");
        Assert.NotNull(finding);
    }

    [Fact]
    public void Scanner_BadImageFormat_Detected()
    {
        var session = ParseFixture("bad-image-format-rph-log.txt");

        var finding = session.Findings.FirstOrDefault(f => f.Code == "bad-image-format");
        Assert.NotNull(finding);
        Assert.Equal(CrashLogSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Scanner_UnhandledException_Detected()
    {
        var session = ParseFixture("unhandled-exception-rph-log.txt");

        Assert.True(
            session.Findings.Any(f =>
                f.Code is "unhandled-plugin-exception" or "plugin-load-failed" or "crash-signal"),
            "Should detect a finding for unhandled exception");
    }

    [Fact]
    public void Scanner_RphHookFailed_DetectedAsFatal()
    {
        var session = ParseFixture("rph-hook-failed-log.txt");

        var finding = session.Findings.FirstOrDefault(f => f.Code == "rph-hook-failed");
        Assert.NotNull(finding);
        Assert.Equal(CrashLogSeverity.Fatal, finding.Severity);
    }

    [Fact]
    public void Scanner_RphHookFailed_HasSuggestedFix()
    {
        var session = ParseFixture("rph-hook-failed-log.txt");

        var finding = session.Findings.First(f => f.Code == "rph-hook-failed");
        Assert.NotEmpty(finding.SuggestedFixes);
    }

    [Fact]
    public void Scanner_ScriptHookVError_DetectedFromShvLog()
    {
        var session = ParseFixture("scripthookv-error-log.txt");

        // SHV log with error lines should produce at least one finding
        Assert.True(session.Findings.Count > 0 ||
                    session.Entries.Any(e => e.Severity == CrashLogSeverity.Error),
            "SHV error log should produce findings or error entries");
    }

    [Fact]
    public void Scanner_ScriptHookVDotNetMissingDep_Detected()
    {
        var session = ParseFixture("scripthookvdotnet-error-log.txt");

        Assert.True(session.Findings.Any(f => f.Code == "missing-dependency"),
            "Should detect missing NativeUI.dll dependency");
    }

    [Fact]
    public void Scanner_MalformedLog_DoesNotCrash()
    {
        var session = ParseFixture("malformed-or-partial-log.txt");

        Assert.NotNull(session);
        // May or may not produce findings — must not throw
    }

    // ── Deduplication ───────────────────────────────────────────────────────

    [Fact]
    public void Scanner_DeduplicatesIdenticalFindings()
    {
        // Use a log that has a repeated pattern
        var lines = Enumerable.Repeat(
            "[11/09/2023 17:20:00.100] [ERROR] [RPH] Could not load file or assembly 'SomeDep.dll'",
            5).ToArray();
        var session = new RageLogParser().Parse("test.log", lines);

        // Manually apply findings via a temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"rph_dedup_{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllLines(tempPath, lines);
            var scanned = new RageLogScanner().ScanFile(tempPath);
            Assert.NotNull(scanned);

            var depFindings = scanned.Findings.Where(f => f.Code == "missing-dependency").ToList();
            // Should be deduplicated to 1
            Assert.Single(depFindings);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // ── Missing file handling ────────────────────────────────────────────────

    [Fact]
    public void Scanner_MissingFile_ReturnsNull()
    {
        var result = new RageLogScanner().ScanFile(
            Path.Combine(Path.GetTempPath(), "nonexistent_rph_log.log"));

        Assert.Null(result);
    }

    // ── Severity assignment ─────────────────────────────────────────────────

    [Fact]
    public void Scanner_FindingsHaveNonInfoSeverityForErrors()
    {
        var session = ParseFixture("missing-dependency-rph-log.txt");

        Assert.True(session.Findings.All(f => f.Severity != CrashLogSeverity.Info),
            "Error findings should not be downgraded to Info severity");
    }
}
