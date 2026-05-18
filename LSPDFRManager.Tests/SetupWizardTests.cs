using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

public class SetupWizardTests : CommandCenterTestBase
{
    [Fact]
    public void ValidPath_ReturnsEmptyError()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var error = new SetupWizardService().ValidatePath(GtaDir);

        Assert.Equal("", error);
    }

    [Fact]
    public void MissingFolder_ReturnsError()
    {
        var error = new SetupWizardService().ValidatePath(@"C:\DoesNotExist\GTA5");

        Assert.NotEmpty(error);
    }

    [Fact]
    public void FolderExistsButNoExe_ReturnsError()
    {
        var error = new SetupWizardService().ValidatePath(GtaDir);

        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidPath_WithOnlyPlayGTAV_ReturnsEmptyError()
    {
        File.WriteAllText(Path.Combine(GtaDir, "PlayGTAV.exe"), "");

        var error = new SetupWizardService().ValidatePath(GtaDir);

        Assert.Equal("", error);
    }

    [Fact]
    public void ValidPath_WithOnlyGTA5BE_ReturnsEmptyError()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5_BE.exe"), "");

        var error = new SetupWizardService().ValidatePath(GtaDir);

        Assert.Equal("", error);
    }

    [Fact]
    public void DetectPaths_DoesNotThrow()
    {
        var candidates = new SetupWizardService().DetectGamePaths();
        Assert.NotNull(candidates);
    }

    [Fact]
    public void GameVersion_NullVersion_WhenExeMissing()
    {
        var info = new GameVersionService().GetCurrentVersion();

        Assert.Null(info.Version);
        Assert.False(info.ChangedSinceLastCheck);
    }

    [Fact]
    public async Task UpdateCheck_ReturnsValidResult()
    {
        var result = await new UpdateCheckService().CheckAsync();

        Assert.NotNull(result);
        Assert.Equal("3.7.15", result.CurrentVersion);
    }

    [Fact]
    public void ScanDirectory_InvalidFolder_NotValidRoot()
    {
        var result = new SetupWizardService().ScanDirectory(@"C:\DoesNotExist\GTA5");

        Assert.False(result.IsValidGtaRoot);
        Assert.False(result.HasLspdfrCore);
        Assert.False(result.HasRagePluginHook);
    }

    [Fact]
    public void ScanDirectory_ValidFolderNoLspdfr_IsValidRoot()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var result = new SetupWizardService().ScanDirectory(GtaDir);

        Assert.True(result.IsValidGtaRoot);
        Assert.False(result.HasLspdfrCore);
        Assert.False(result.HasRagePluginHook);
    }

    [Fact]
    public void ScanDirectory_FullInstall_HasLspdfrAndRph()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        Directory.CreateDirectory(Path.Combine(GtaDir, "Plugins", "LSPD First Response"));
        File.WriteAllText(Path.Combine(GtaDir, "Plugins", "LSPD First Response.dll"), "");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "");
        File.WriteAllText(Path.Combine(GtaDir, "RagePluginHook.dll"), "");

        var result = new SetupWizardService().ScanDirectory(GtaDir);

        Assert.True(result.IsValidGtaRoot);
        Assert.True(result.HasLspdfrCore);
        Assert.True(result.HasRagePluginHook);
        Assert.True(result.HasRagePluginHookDll);
        Assert.True(result.IsReadyForLspdfr);
    }

    [Fact]
    public void SetupWizardViewModel_Finish_FiresOnFinished()
    {
        AppConfig.Instance.GtaPath = GtaDir;
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var vm = new SetupWizardViewModel();
        var fired = false;
        vm.OnFinished = () => fired = true;

        vm.FinishCommand.Execute(null);

        Assert.True(fired);
    }
}
