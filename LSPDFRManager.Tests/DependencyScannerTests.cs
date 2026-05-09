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
}
