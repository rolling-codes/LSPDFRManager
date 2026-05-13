using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for InstallViewModel command guards, property transitions, and error surfacing.
/// UiDispatcher falls through to direct invocation in xUnit (no Application.Current),
/// so INPC changes are synchronous in tests.
/// </summary>
[Collection("AppData serial")]
public class InstallViewModelTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_ivm_{Guid.NewGuid():N}");

    public InstallViewModelTests()
    {
        // Redirect AppData so InstallQueue/ModLibraryService don't touch real filesystem
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        AppConfig.Instance.GtaPath = Path.Combine(_tempRoot, "GTA");
        Directory.CreateDirectory(AppConfig.Instance.GtaPath);
    }

    public void Dispose()
    {
        try { ModLibraryService.Instance.Mods.Clear(); } catch { }
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void InstallCommand_CannotExecute_WhenDetectedModIsNull()
    {
        // Arrange
        var vm = new InstallViewModel();

        // Act — DetectedMod is null by default

        // Assert
        Assert.False(vm.InstallCommand.CanExecute(null));
    }

    [Fact]
    public void InstallCommand_CannotExecute_WhenIsInstallingIsTrue()
    {
        // Arrange
        var vm = new InstallViewModel();
        vm.DetectedMod = new ModInfo { Name = "TestMod", Type = ModType.AsiMod };

        // Act
        vm.IsInstalling = true;

        // Assert
        Assert.False(vm.InstallCommand.CanExecute(null));
    }

    [Fact]
    public void InstallCommand_CanExecute_WhenDetectedModSetAndNotInstalling()
    {
        // Arrange
        var vm = new InstallViewModel();

        // Act
        vm.DetectedMod = new ModInfo { Name = "TestMod", Type = ModType.AsiMod };

        // Assert
        Assert.True(vm.InstallCommand.CanExecute(null));
    }

    [Fact]
    public void IsIdle_FalseWhenDetecting()
    {
        // Arrange
        var vm = new InstallViewModel();

        // Act
        vm.IsDetecting = true;

        // Assert
        Assert.False(vm.IsIdle);
    }

    [Fact]
    public void IsIdle_FalseWhenInstalling()
    {
        // Arrange
        var vm = new InstallViewModel();

        // Act
        vm.IsInstalling = true;

        // Assert
        Assert.False(vm.IsIdle);
    }

    [Fact]
    public void IsIdle_TrueWhenNeitherDetectingNorInstalling()
    {
        // Arrange + Act
        var vm = new InstallViewModel();

        // Assert
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public async Task DetectAsync_SetsIsDetectingFalse_InFinally_EvenOnInvalidPath()
    {
        // Arrange
        var vm = new InstallViewModel();
        var badPath = Path.Combine(_tempRoot, "not_an_archive.txt");
        File.WriteAllText(badPath, "not an archive");

        // Act — detect a non-archive; may throw internally but finally must run
        try { await vm.DetectAsync(badPath); } catch { /* expected */ }

        // Assert
        Assert.False(vm.IsDetecting, "IsDetecting should reset to false in finally block");
    }

    [Fact]
    public async Task DetectAsync_WithValidZip_SetsDetectedMod()
    {
        // Arrange
        var vm = new InstallViewModel();
        var zipPath = Path.Combine(_tempRoot, "ScriptHook.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntry("ScriptHookV.asi").Open().Close();

        // Act
        await vm.DetectAsync(zipPath);

        // Assert
        Assert.NotNull(vm.DetectedMod);
    }

    [Fact]
    public void ClearCommand_ResetsState()
    {
        // Arrange
        var vm = new InstallViewModel();
        vm.DetectedMod = new ModInfo { Name = "TestMod" };
        vm.NameOverride = "Override";

        // Act
        vm.ClearCommand.Execute(null);

        // Assert
        Assert.Null(vm.DetectedMod);
        Assert.Equal("", vm.NameOverride);
    }

    [Fact]
    public void LastErrorMessage_HasLastError_ReflectsNonNull()
    {
        // Arrange
        var vm = new InstallViewModel();

        // Act
        vm.LastErrorMessage = "Something went wrong";

        // Assert
        Assert.True(vm.HasLastError);
    }

    [Fact]
    public void LastErrorMessage_HasLastError_FalseWhenNull()
    {
        // Arrange + Act
        var vm = new InstallViewModel();

        // Assert
        Assert.False(vm.HasLastError);
    }
}
