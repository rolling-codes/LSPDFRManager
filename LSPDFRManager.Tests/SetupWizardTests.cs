using LSPDFRManager.Services;
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
        Assert.Equal("3.7.14", result.CurrentVersion);
    }
}
