using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class LspdfrInstallLocatorTests : CommandCenterTestBase
{
    [Fact]
    public void StatusServices_DetectOfficialLspdfrLayout()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "fake");
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins"));
        Directory.CreateDirectory(Path.Combine(GtaDir, "lspdfr"));
        File.WriteAllText(Path.Combine(GtaDir, "plugins", "LSPD First Response.dll"), "fake");

        AppConfig.Instance.GtaPath = GtaDir;
        LspdfrStatusService.Instance.Refresh();
        DashboardStatusService.Instance.Refresh();

        Assert.True(LspdfrStatusService.Instance.IsGtaPathValid);
        Assert.True(LspdfrStatusService.Instance.IsLspdfrInstalled);
        Assert.True(DashboardStatusService.Instance.IsGtaPathValid);
        Assert.True(DashboardStatusService.Instance.IsLspdfrInstalled);
        Assert.True(DashboardStatusService.Instance.IsRagePluginHookInstalled);
    }

    [Fact]
    public void Locator_FindsLspdfrToolInRootSupportFolder()
    {
        var supportDir = Path.Combine(GtaDir, "lspdfr");
        Directory.CreateDirectory(supportDir);
        var tool = Path.Combine(supportDir, "LSPDFR Configurator.exe");
        File.WriteAllText(tool, "fake");

        Assert.Equal(tool, LspdfrInstallLocator.FindLspdfrTool(GtaDir));
        Assert.True(LspdfrInstallLocator.IsLspdfrInstalled(GtaDir));
    }

    [Fact]
    public void FindGtaExe_ReturnsGTA5BE_WhenOnlyThatExists()
    {
        var expected = Path.Combine(GtaDir, "GTA5_BE.exe");
        File.WriteAllText(expected, "fake");

        var result = LspdfrInstallLocator.FindGtaExe(GtaDir);

        Assert.Equal(expected, result);
        Assert.True(LspdfrInstallLocator.IsGtaInstalled(GtaDir));
    }

    [Fact]
    public void FindGtaExe_ReturnsPlayGTAV_WhenOnlyThatExists()
    {
        var expected = Path.Combine(GtaDir, "PlayGTAV.exe");
        File.WriteAllText(expected, "fake");

        var result = LspdfrInstallLocator.FindGtaExe(GtaDir);

        Assert.Equal(expected, result);
        Assert.True(LspdfrInstallLocator.IsGtaInstalled(GtaDir));
    }

    [Fact]
    public void FindGtaExe_PrefersGTA5Exe_WhenMultipleCandidatesExist()
    {
        var gta5 = Path.Combine(GtaDir, "GTA5.exe");
        File.WriteAllText(gta5, "fake");
        File.WriteAllText(Path.Combine(GtaDir, "GTA5_BE.exe"), "fake");
        File.WriteAllText(Path.Combine(GtaDir, "PlayGTAV.exe"), "fake");

        var result = LspdfrInstallLocator.FindGtaExe(GtaDir);

        Assert.Equal(gta5, result);
    }
}
