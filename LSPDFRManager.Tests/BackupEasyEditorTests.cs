using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class BackupEasyEditorTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "BEETests_" + Guid.NewGuid());

    public BackupEasyEditorTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }

    // ── Discovery ──────────────────────────────────────────────────────────────

    [Fact]
    public void BackupDiscovery_FindsXmlUnderUltimateBackupFolder()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "backup.xml"), "<root/>");

        var svc = new BackupConfigDiscoveryService(_tmp);
        var results = svc.DiscoverBackupXmlFiles();

        Assert.Contains(results, r => r.EndsWith("backup.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BackupDiscovery_EmptyPath_ReturnsEmpty_DoesNotThrow()
    {
        var svc = new BackupConfigDiscoveryService("");
        var results = svc.DiscoverBackupXmlFiles();
        Assert.Empty(results);
    }

    // ── Parser ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BackupXmlParser_InvalidXml_ReturnsEmpty_DoesNotThrow()
    {
        var file = Path.Combine(_tmp, "bad.xml");
        File.WriteAllText(file, "<<not valid xml>>");
        var result = BackupXmlParser.Parse(file);
        Assert.Empty(result);
    }

    [Fact]
    public void BackupXmlParser_UnknownStructure_ReturnsUnsupportedFinding()
    {
        var file = Path.Combine(_tmp, "unknown.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <MyCustomData>
              <Row col1="foo" col2="bar" />
            </MyCustomData>
            """);
        var finding = BackupXmlParser.Diagnose(file);
        Assert.NotNull(finding);
        Assert.Contains("unsupported or unknown structure", finding!.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void BackupXmlParser_SimpleUnitXml_ParsesAgencyAndPedModel()
    {
        var file = Path.Combine(_tmp, "units.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="LSPD" UnitType="LocalPatrol" PedModel="S_M_Y_Cop_01" VehicleModel="POLICE" />
              <Unit Agency="BCSO" UnitType="LocalPatrol" PedModel="S_M_Y_Sheriff_01" VehicleModel="SHERIFF" />
            </BackupUnits>
            """);

        var units = BackupXmlParser.Parse(file);
        Assert.Equal(2, units.Count);
        Assert.Contains(units, u => u.Agency == "LSPD" && u.PedModel == "S_M_Y_Cop_01");
        Assert.Contains(units, u => u.Agency == "BCSO" && u.PedModel == "S_M_Y_Sheriff_01");
    }

    [Fact]
    public void BackupXmlParser_FileNotFound_ReturnsEmpty_DoesNotThrow()
    {
        var result = BackupXmlParser.Parse(Path.Combine(_tmp, "nonexistent.xml"));
        Assert.Empty(result);
    }

    // ── Editor validation ──────────────────────────────────────────────────────

    [Fact]
    public void BackupEasyEditor_NoFilesFound_ReturnsInfoFinding()
    {
        var svc = new BackupEasyEditorService(_tmp);
        var findings = svc.ValidateBackupConfigs();
        Assert.Contains(findings, f => f.Title.Contains("No backup XML files found"));
        Assert.All(findings.Where(f => f.Title.Contains("No backup XML files found")),
            f => Assert.Equal(DiagnosticSeverity.Info, f.Severity));
    }

    [Fact]
    public void BackupEasyEditor_ValidFile_ReturnsFoundFinding()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "units.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="LSPD" PedModel="S_M_Y_Cop_01" />
            </BackupUnits>
            """);

        var svc = new BackupEasyEditorService(_tmp);
        var findings = svc.ValidateBackupConfigs();
        Assert.Contains(findings, f => f.Title.Contains("Backup config file found"));
    }

    [Fact]
    public void BackupEasyEditor_InvalidXml_DoesNotThrow()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.xml"), "<<broken");

        var svc = new BackupEasyEditorService(_tmp);
        var findings = svc.ValidateBackupConfigs();
        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("Backup XML could not be read", StringComparison.OrdinalIgnoreCase));
    }

    // ── Preview ────────────────────────────────────────────────────────────────

    [Fact]
    public void PreviewUniformPatch_DoesNotWriteFile()
    {
        var file = Path.Combine(_tmp, "preview.xml");
        var originalContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="LSPD" PedModel="S_M_Y_Cop_01" />
            </BackupUnits>
            """;
        File.WriteAllText(file, originalContent);

        var mapping = new BackupUniformMapping { Agency = "LSPD", DisplayName = "LSPD Officer" };
        var svc = new BackupEasyEditorService(_tmp);
        var preview = svc.PreviewUniformPatch(file, mapping);

        Assert.Equal(originalContent, File.ReadAllText(file));
        Assert.True(preview.CanApply);
        Assert.NotEmpty(preview.BeforeLines);
        Assert.NotEmpty(preview.AfterLines);
    }

    [Fact]
    public void BackupXmlParser_EmptyFile_DoesNotThrow()
    {
        var path = Path.Combine(_tmp, "empty.xml");
        File.WriteAllText(path, "");
        var units = BackupXmlParser.Parse(path);
        var finding = BackupXmlParser.Diagnose(path);
        Assert.Empty(units);
        Assert.NotNull(finding);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains("could not be read", finding.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BackupXmlParser_NonXmlTextFile_DoesNotThrow()
    {
        var path = Path.Combine(_tmp, "notxml.xml");
        File.WriteAllText(path, "this is not xml content at all!!!");
        var units = BackupXmlParser.Parse(path);
        var finding = BackupXmlParser.Diagnose(path);
        Assert.Empty(units);
        Assert.NotNull(finding);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains("could not be read", finding.Title, StringComparison.OrdinalIgnoreCase);
    }
}
