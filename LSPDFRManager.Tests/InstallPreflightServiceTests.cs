using LSPDFRManager.Models;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class InstallPreflightServiceTests
{
    [Fact]
    public void Evaluate_MissingGtaPath_ReturnsBlockingWarning()
    {
        var warnings = InstallPreflightService.Evaluate(@"Z:\does-not-exist", null);
        Assert.Contains(warnings, w => w.Contains("missing or invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_ScriptModWithoutScriptHook_AddsScriptHookWarning()
    {
        var gtaPath = Directory.CreateTempSubdirectory("lspdfr-preflight").FullName;
        File.WriteAllText(Path.Combine(gtaPath, "RAGEPluginHook.exe"), "stub");
        Directory.CreateDirectory(Path.Combine(gtaPath, "Plugins", "LSPDFR"));
        File.WriteAllText(Path.Combine(gtaPath, "Plugins", "LSPDFR", "LSPD First Response.dll"), "stub");

        var warnings = InstallPreflightService.Evaluate(gtaPath, new ModInfo { Type = ModType.Script });

        Assert.Contains(warnings, w => w.Contains("ScriptHookV.dll", StringComparison.OrdinalIgnoreCase));
    }
}
