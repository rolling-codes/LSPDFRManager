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
    public void LspdfrOfficialLayout_DoesNotReportCoreMissing()
    {
        CreateFile("plugins/LSPD First Response.dll");
        CreateDir("lspdfr");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Category == "Dependencies" &&
            f.Title.Contains("LSPDFR Core") &&
            f.Title.Contains("missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(findings, f =>
            f.Category == "Install" &&
            f.Title.Contains("LSPDFR Core") &&
            f.Title.Contains("folder missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidRphLspdfrInstall_WithoutScriptHookV_NoScriptHookVError()
    {
        // A correct RagePluginHook + LSPDFR install with NO ASI mods. ScriptHookV /
        // dinput8 are absent — RPH/LSPDFR do not require them, so this must not be a
        // blocking Error (Warning is acceptable).
        CreateFile("RAGEPluginHook.exe");
        CreateFile("RagePluginHook.dll");
        CreateFile("plugins/LSPD First Response.dll");
        CreateDir("lspdfr");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("ScriptHookV", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RphMissingDll_ReturnsErrorFinding()
    {
        // RAGEPluginHook.exe is present but RagePluginHook.dll is missing
        CreateFile("RAGEPluginHook.exe");
        CreateFile("plugins/LSPD First Response.dll");
        CreateDir("lspdfr");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("RAGE Plugin Hook file missing", StringComparison.OrdinalIgnoreCase) &&
            f.Detail.Contains("RagePluginHook.dll", StringComparison.OrdinalIgnoreCase));
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


    [Fact]
    public void LspdfrCoreLegacyAliasOnly_NoWrongFolderError()
    {
        // S2: plugins/LSPDFR.dll is a TOLERATED legacy alias for the LSPDFR core,
        // not a wrong-folder error, even when the canonical
        // plugins/LSPD First Response.dll is absent.
        CreateFile("plugins/LSPDFR.dll");
        CreateDir("lspdfr");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("LSPDFR Core", StringComparison.OrdinalIgnoreCase) &&
            f.Title.Contains("wrong folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RageNativeUiAtGtaRootOnly_NoErrorOrMissing()
    {
        // Canonical location per the RAGENativeUI author.
        CreateFile("RageNativeUI.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Title.Contains("RageNativeUI", StringComparison.OrdinalIgnoreCase) &&
            (f.Severity == DiagnosticSeverity.Error ||
             f.Title.Contains("missing", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void RageNativeUiInPluginsOnly_NoErrorOrMissing()
    {
        // Tolerated alternate location (back-compat with plugins/-shipped packages).
        CreateFile("plugins/RageNativeUI.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Title.Contains("RageNativeUI", StringComparison.OrdinalIgnoreCase) &&
            (f.Severity == DiagnosticSeverity.Error ||
             f.Title.Contains("missing", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void RageNativeUiInPluginsLspdfrOnly_ReturnsWrongFolderError()
    {
        // Neither accepted location present — plugins/lspdfr/ is still wrong.
        CreateFile("plugins/lspdfr/RageNativeUI.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Error &&
            f.Title.Contains("RageNativeUI", StringComparison.OrdinalIgnoreCase) &&
            f.Title.Contains("wrong folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UltimateBackupWithoutStopThePed_ReturnsDependencyWarning()
    {
        CreateFile("plugins/lspdfr/UltimateBackup.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("Ultimate Backup", StringComparison.OrdinalIgnoreCase) &&
            f.Detail.Contains("Stop The Ped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UltimateBackupWithStopThePed_DoesNotReturnMissingStopThePedWarning()
    {
        CreateFile("plugins/lspdfr/UltimateBackup.dll");
        CreateFile("plugins/lspdfr/StopThePed.dll");

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.DoesNotContain(findings, f =>
            f.Title.Contains("Ultimate Backup", StringComparison.OrdinalIgnoreCase) &&
            f.Detail.Contains("Stop The Ped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UltimateBackupTransportReferenceWithoutStopThePed_ReturnsConfigWarning()
    {
        CreateFile("plugins/lspdfr/UltimateBackup.dll");
        CreateFile("plugins/lspdfr/UltimateBackup.ini");

        File.WriteAllText(
            Path.Combine(_tempDir, "plugins", "lspdfr", "UltimateBackup.ini"),
            """
            [Transport]
            CoronerUnit=Enabled
            """);

        var findings = new RecipeValidatorService(_tempDir).Validate();

        Assert.Contains(findings, f =>
            f.Severity == DiagnosticSeverity.Warning &&
            f.Title.Contains("may require Stop The Ped", StringComparison.OrdinalIgnoreCase));
    }

}
