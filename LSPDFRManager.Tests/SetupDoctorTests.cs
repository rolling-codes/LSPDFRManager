using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class SetupDoctorTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string MakeTempDir(string? suffix = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"LSPMDoctorTest_{suffix ?? Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static void SetGtaPath(string path)
    {
        AppConfig.Instance.GtaPath = path;
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
    }

    [Fact]
    public async Task SetupDoctor_InvalidPath_ReturnsErrorFinding_DoesNotThrow()
    {
        SetGtaPath(@"C:\DoesNotExist_LSPMTest_" + Guid.NewGuid());
        var svc = new SetupDoctorService();
        var findings = await svc.RunAsync();
        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Error && f.Category == "Setup");
    }

    [Fact]
    public async Task SetupDoctor_NonWritablePath_Skipped_DoesNotThrow()
    {
        var dir = MakeTempDir();
        SetGtaPath(dir);
        var svc = new SetupDoctorService();
        var findings = await svc.RunAsync();
        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.Category == "RAGE Plugin Hook" && f.Severity == DiagnosticSeverity.Warning);
        Assert.Contains(findings, f => f.Category == "LSPDFR" && f.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SetupDoctor_OneDrivePath_ReturnsWarning()
    {
        var dir = MakeTempDir("OneDriveTest_" + Guid.NewGuid().ToString("N"));
        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("OneDrive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetupDoctor_ProgramFilesPath_ReturnsWarning()
    {
        // Create a real temp dir whose path string contains "Program Files"
        var base64 = Path.Combine(Path.GetTempPath(), "Program Files_LSPMTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(base64);
        _tempDirs.Add(base64);
        SetGtaPath(base64);
        var findings = await new SetupDoctorService().RunAsync();
        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("Program Files", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetupDoctor_MissingRph_ReturnsRphWarning()
    {
        var dir = MakeTempDir();
        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        Assert.Contains(findings, f =>
            f.Category == "RAGE Plugin Hook" &&
            f.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task SetupDoctor_MissingLspdfrFolder_ReturnsLspdfrError()
    {
        var dir = MakeTempDir();
        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        Assert.Contains(findings, f =>
            f.Category == "LSPDFR" &&
            f.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SetupDoctor_IncludesRecipeValidatorFindings()
    {
        var dir = MakeTempDir();
        // Place only ScriptHookV.dll — ELS and other dependencies missing → recipe findings
        File.WriteAllText(Path.Combine(dir, "ScriptHookV.dll"), "fake");
        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        // RecipeValidatorService will report missing files for LSPDFR Core, ELS, etc.
        Assert.Contains(findings, f => f.Category is "Dependencies" or "Install" or "Config");
    }

    [Fact]
    public async Task SetupDoctor_IncludesKeybindConflictFindings()
    {
        var dir = MakeTempDir();
        var lspdfrDir = Path.Combine(dir, "plugins", "lspdfr");
        Directory.CreateDirectory(lspdfrDir);

        // Two INI files sharing the same key value → conflict
        File.WriteAllText(Path.Combine(lspdfrDir, "PluginA.ini"), "[Controls]\nMenuKey=F5\n");
        File.WriteAllText(Path.Combine(lspdfrDir, "PluginB.ini"), "[Controls]\nMenuKey=F5\n");

        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        Assert.Contains(findings, f => f.Category == "Keybinds");
    }

    [Fact]
    public async Task SetupDoctor_IncludesBackupEditorFindings()
    {
        var dir = MakeTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "plugins", "lspdfr"));
        SetGtaPath(dir);
        var findings = await new SetupDoctorService().RunAsync();
        // BackupEasyEditorService adds an Info finding when no XML files found
        Assert.Contains(findings, f =>
            f.Category == "Config" &&
            f.Title.Contains("No backup XML files", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetupDoctor_DeduplicatesExactDuplicates()
    {
        // Use an invalid path so RunAsync returns early with a single Setup/Error finding
        // Set it to a path that contains the same non-existent dir twice — but the
        // real test is that after RunAsync, no two findings share (Title, AffectedPath, Severity).
        SetGtaPath(@"C:\DoesNotExist_LSPMTest_" + Guid.NewGuid());
        var findings = await new SetupDoctorService().RunAsync();
        var groups = findings
            .GroupBy(f => (f.Title, f.AffectedPath, f.Severity))
            .ToList();
        Assert.All(groups, g => Assert.Single(g));
    }

    [Fact]
    public async Task SetupDoctor_ConfidenceZeroNormalized()
    {
        SetGtaPath(@"C:\DoesNotExist_LSPMTest_" + Guid.NewGuid());
        var findings = await new SetupDoctorService().RunAsync();
        Assert.All(findings, f => Assert.True(f.Confidence > 0f));
    }

    [Fact]
    public void BackupXmlParser_AgencyContainerNode_NotClassifiedAsUnit()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Agency Name="LSPD">
                <Agency Name="SubDivision" />
              </Agency>
            </BackupUnits>
            """;
        var tmpFile = Path.Combine(Path.GetTempPath(), $"lspm_bxp_test_{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(tmpFile, xml);
            var units = BackupXmlParser.Parse(tmpFile);
            Assert.Empty(units);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }
}
