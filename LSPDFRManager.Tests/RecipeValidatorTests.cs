using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class RecipeValidatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public RecipeValidatorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private void CreateFile(string relPath)
    {
        var full = Path.Combine(_tempDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
    }

    private void CreateDir(string relPath)
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, relPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void ValidGtaLayout_NoFindings_WhenAllFilesPresent()
    {
        // ELS complete layout
        CreateFile("ELS.asi");
        CreateFile("ELS.ini");
        CreateFile("AdvancedHookV.dll");
        CreateDir("ELS");
        CreateFile("ScriptHookV.dll");
        CreateFile("dinput8.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        var elsErrors = findings.Where(f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("ELS")).ToList();

        Assert.Empty(elsErrors);
    }

    [Fact]
    public void ElsMissingAsi_ReturnsErrorFinding()
    {
        // Only ini and folder, no .asi
        CreateFile("ELS.ini");
        CreateDir("ELS");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("ELS"));
    }

    [Fact]
    public void ElsInWrongFolder_ReturnsErrorFinding()
    {
        // ELS.asi under plugins/ instead of root
        CreateFile("plugins/ELS.asi");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("ELS") &&
            (f.Detail.Contains("plugins/ELS.asi") || f.AffectedPath!.Contains("plugins")));
    }

    [Fact]
    public void UltimateBackup_DllPresentIniMissing_ReturnsWarning()
    {
        CreateFile("plugins/lspdfr/UltimateBackup.dll");
        // intentionally no .ini

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("config", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidGtaPath_ReturnsErrorFinding_DoesNotThrow()
    {
        var svc = new RecipeValidatorService(@"C:\nonexistent\path\99999");

        var ex = Record.Exception(() => svc.Validate());
        Assert.Null(ex);

        var findings = svc.Validate();
        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void DuplicateRageNativeUi_ReturnsWarning()
    {
        CreateFile("plugins/RageNativeUI.dll");          // correct path
        CreateFile("plugins/lspdfr/RageNativeUI.dll");   // wrong path (duplicate)

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
