using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class DependencyScannerTests : CommandCenterTestBase
{
    [Fact]
    public void Gta5Exe_InstalledWhenPresent()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Installed, results.First(r => r.Name == "GTA5.exe").Status);
    }

    [Fact]
    public void Gta5Exe_MissingWhenAbsent()
    {
        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Missing, results.First(r => r.Name == "GTA5.exe").Status);
    }

    [Fact]
    public void DetectsDisabledDependency()
    {
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookV.dll.disabled"), "");

        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Disabled, results.First(r => r.Name == "ScriptHookV.dll").Status);
    }


    [Fact]
    public void RagePluginHookDll_EntryExistsInResults()
    {
        var results = new DependencyScanner().Scan();
        Assert.Contains(results, r => r.Name == "RagePluginHook.dll");
    }

    [Fact]
    public void RagePluginHookDll_InstalledWhenPresent()
    {
        File.WriteAllText(Path.Combine(GtaDir, "RagePluginHook.dll"), "");
        var results = new DependencyScanner().Scan();
        Assert.Equal(DependencyStatus.Installed, results.First(r => r.Name == "RagePluginHook.dll").Status);
    }

    [Fact]
    public void RagePluginHookDll_MissingWhenAbsent()
    {
        var results = new DependencyScanner().Scan();
        Assert.Equal(DependencyStatus.Missing, results.First(r => r.Name == "RagePluginHook.dll").Status);
    }

    [Fact]
    public void RagePluginHookExe_MissingWhenAbsent()
    {
        var results = new DependencyScanner().Scan();
        Assert.Equal(DependencyStatus.Missing, results.First(r => r.Name == "RAGEPluginHook.exe").Status);
    }

    [Fact]
    public void RagePluginHook_MissingNote_IndicatesLspdfrLaunch()
    {
        var results = new DependencyScanner().Scan();
        var dll = results.First(r => r.Name == "RagePluginHook.dll");
        var exe = results.First(r => r.Name == "RAGEPluginHook.exe");
        Assert.Contains("LSPDFR", dll.Note ?? "");
        Assert.Contains("LSPDFR", exe.Note ?? "");
    }

    [Fact]
    public void StopThePedAndUltimateBackup_DetectedWhenPresent()
    {
        var pluginDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "StopThePed.dll"), "");
        File.WriteAllText(Path.Combine(pluginDir, "UltimateBackup.dll"), "");

        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Installed, results.First(r => r.Name == "StopThePed.dll").Status);
        Assert.Equal(DependencyStatus.Installed, results.First(r => r.Name == "UltimateBackup.dll").Status);
    }

}
