using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("CommandCenter")]
public class CleanupScannerTests : CommandCenterTestBase
{


    // Test 1
    [Fact]
    public void Scan_LspdfrCoreDll_ClassifiedAsLspdfrCore()
    {
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins"));
        File.WriteAllText(Path.Combine(GtaDir, "plugins", "LSPD First Response.dll"), "");

        var result = LspdfrCleanupScanner.Scan(GtaDir);

        var group = result.Groups.FirstOrDefault(g => g.GroupKind == CandidateClassification.LspdfrCore);
        Assert.NotNull(group);
        Assert.Contains(group.Candidates, c => c.Classification == CandidateClassification.LspdfrCore);
    }

    // Test 2
    [Fact]
    public void Scan_LspdfrFolder_ClassifiedAsLspdfrData()
    {
        Directory.CreateDirectory(Path.Combine(GtaDir, "lspdfr"));

        var result = LspdfrCleanupScanner.Scan(GtaDir);

        var group = result.Groups.FirstOrDefault(g => g.GroupKind == CandidateClassification.LspdfrData);
        Assert.NotNull(group);
        Assert.Single(group.Candidates);
        Assert.True(group.Candidates[0].IsDirectory);
    }

    // Test 3
    [Fact]
    public void Scan_ThirdPartyPluginDll_ClassifiedAsThirdPartyPlugin()
    {
        var pluginDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "MyPlugin.dll"), "");

        var result = LspdfrCleanupScanner.Scan(GtaDir);

        var group = result.Groups.FirstOrDefault(g => g.Label == "MyPlugin");
        Assert.NotNull(group);
        Assert.Contains(group.Candidates, c => c.Classification == CandidateClassification.ThirdPartyPlugin);
    }

    // Test 4
    [Fact]
    public void Scan_PluginIni_GroupedWithDll()
    {
        var pluginDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "MyPlugin.dll"), "");
        File.WriteAllText(Path.Combine(pluginDir, "MyPlugin.ini"), "");

        var result = LspdfrCleanupScanner.Scan(GtaDir);

        var group = result.Groups.FirstOrDefault(g => g.Label == "MyPlugin");
        Assert.NotNull(group);
        Assert.Contains(group.Candidates, c => c.Classification == CandidateClassification.ThirdPartyPlugin);
        Assert.Contains(group.Candidates, c => c.Classification == CandidateClassification.PluginConfig);
    }

    // Test 5
    [Fact]
    public void Scan_Albo1125Common_ClassifiedAsSharedDependency()
    {
        var pluginLspdfrDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginLspdfrDir);
        File.WriteAllText(Path.Combine(pluginLspdfrDir, "Albo1125.Common.dll"), "");

        var result = LspdfrCleanupScanner.Scan(GtaDir);

        var group = result.Groups.FirstOrDefault(g => g.GroupKind == CandidateClassification.SharedDependency);
        Assert.NotNull(group);
        Assert.Contains(group.Candidates,
            c => Path.GetFileName(c.FullPath).Equals("Albo1125.Common.dll", StringComparison.OrdinalIgnoreCase));
    }

    // Test 6
    [Fact]
    public void IsGtaExecutable_GtaExes_ReturnTrue()
    {
        Assert.True(LspdfrCleanupScanner.IsGtaExecutable("GTA5.exe"));
        Assert.True(LspdfrCleanupScanner.IsGtaExecutable("GTA5_BE.exe"));
        Assert.True(LspdfrCleanupScanner.IsGtaExecutable("PlayGTAV.exe"));
        Assert.True(LspdfrCleanupScanner.IsGtaExecutable(Path.Combine("C:", "Games", "GTA5.exe")));
    }

    // Test 7
    [Fact]
    public void IsOutsideRoot_OutsidePath_ReturnsTrue()
    {
        Assert.True(LspdfrCleanupScanner.IsOutsideRoot(GtaDir, Path.Combine(TempDir, "outside.dll")));
    }

    [Fact]
    public void IsOutsideRoot_InsidePath_ReturnsFalse()
    {
        Assert.False(LspdfrCleanupScanner.IsOutsideRoot(GtaDir, Path.Combine(GtaDir, "plugins", "test.dll")));
    }
}
