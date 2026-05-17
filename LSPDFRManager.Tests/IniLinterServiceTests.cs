using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class IniLinterServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IniLinterService _svc = new();

    public IniLinterServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"linter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteIni(string content)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, content);
        return path;
    }

    // ── Supports ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("config.ini", true)]
    [InlineData("config.cfg", true)]
    [InlineData("config.xml", true)]
    [InlineData("config.json", true)]
    [InlineData("config.meta", true)]
    [InlineData("config.dll", false)]
    public void Supports_ReturnsCorrectly(string fileName, bool expected)
    {
        Assert.Equal(expected, _svc.Supports(fileName));
    }

    // ── INI linting ───────────────────────────────────────────────────────────

    [Fact]
    public void CleanIni_ProducesNoFindings()
    {
        var path = WriteIni("[Settings]\nEnableFeature=true\nTimeout=30\n");
        var findings = _svc.Lint(path);
        Assert.Empty(findings);
    }

    [Fact]
    public void DuplicateKey_Detected()
    {
        var path = WriteIni("[Settings]\nKey=1\nKey=2\n");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "duplicate-key");
    }

    [Fact]
    public void EmptyValue_Reported()
    {
        var path = WriteIni("[Settings]\nMyKey=\n");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "empty-value");
    }

    [Fact]
    public void BadBoolean_Detected()
    {
        var path = WriteIni("[Settings]\nEnableFeature=enabled\n");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "bad-boolean");
    }

    [Fact]
    public void MalformedLine_Detected()
    {
        var path = WriteIni("[Settings]\nthislinehasonoequalssign\n");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "malformed-line");
    }

    [Fact]
    public void DuplicateKey_FindingHasLineNumber()
    {
        var path = WriteIni("[Settings]\nKey=1\nKey=2\n");
        var findings = _svc.Lint(path);
        var dup = findings.First(f => f.Code == "duplicate-key");
        Assert.NotNull(dup.Line);
        Assert.Equal(3, dup.Line); // line 3 is the second "Key=2"
    }

    // ── XML linting ───────────────────────────────────────────────────────────

    [Fact]
    public void ValidXml_ProducesNoFindings()
    {
        var path = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(path, "<root><item>value</item></root>");
        var findings = _svc.Lint(path);
        Assert.Empty(findings);
    }

    [Fact]
    public void InvalidXml_Detected()
    {
        var path = Path.Combine(_tempDir, "bad.xml");
        File.WriteAllText(path, "<root><unclosed>");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "invalid-xml");
        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Error);
    }

    // ── JSON linting ──────────────────────────────────────────────────────────

    [Fact]
    public void ValidJson_ProducesNoFindings()
    {
        var path = Path.Combine(_tempDir, "test.json");
        File.WriteAllText(path, "{\"key\": \"value\"}");
        var findings = _svc.Lint(path);
        Assert.Empty(findings);
    }

    [Fact]
    public void InvalidJson_Detected()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "{\"key\": }");
        var findings = _svc.Lint(path);
        Assert.Contains(findings, f => f.Code == "invalid-json");
    }

    [Fact]
    public void MissingFile_ReturnsUnreadableFinding()
    {
        var findings = _svc.Lint(Path.Combine(_tempDir, "nonexistent.ini"));
        Assert.Contains(findings, f => f.Code == "unreadable");
    }
}
