using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using System.Windows;
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
    private sealed class TestPromptService : IUserPromptService
    {
        public bool NextSelectResult { get; set; }
        public string NextFileName { get; set; } = "";
        public MessageBoxResult ShowResult { get; set; } = MessageBoxResult.Yes;

        public bool TrySelectModArchive(out string fileName)
        {
            fileName = NextFileName;
            return NextSelectResult;
        }

        public MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
            ShowResult;
    }

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
    public void InstallCommand_Execute_DoesNotThrow_WhenDetectedModIsNull()
    {
        var vm = new InstallViewModel();
        var ex = Record.Exception(() => vm.InstallCommand.Execute(null));
        Assert.Null(ex);
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
    public async Task DetectAsync_WithAutoInstallEnabled_StillStagesForExplicitInstall()
    {
        var previous = AppConfig.Instance.AutoInstallHighConfidence;
        AppConfig.Instance.AutoInstallHighConfidence = true;
        try
        {
            var vm = new InstallViewModel();
            var zipPath = Path.Combine(_tempRoot, "HighConfidencePlugin.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntry("plugins/LSPDFR/HighConfidencePlugin.dll").Open().Close();

            await vm.DetectAsync(zipPath);

            Assert.NotNull(vm.DetectedMod);
            Assert.Equal(zipPath, vm.DroppedPath);
            Assert.False(vm.IsInstalling);
            Assert.Contains(vm.Log, entry => entry.Contains("Staged for review", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(vm.Log, entry => entry.Contains("Auto-queuing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            AppConfig.Instance.AutoInstallHighConfidence = previous;
        }
    }

    [Fact]
    public async Task BrowseDownloadStage_SetsDetectedMod_WithoutInstalling()
    {
        var vm = new InstallViewModel(new TestPromptService());
        var zipPath = Path.Combine(_tempRoot, "BrowsePlugin.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntry("plugins/LSPDFR/BrowsePlugin.dll").Open().Close();

        await ModDownloadBridge.Instance.StageDownloadAsync(zipPath, "BrowsePlugin.zip");

        Assert.NotNull(vm.DetectedMod);
        Assert.Equal(zipPath, vm.DetectedMod!.SourcePath);
        Assert.False(vm.IsInstalling);
        Assert.Contains(vm.Log, entry => entry.Contains("Staged for review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BrowseDownloadStage_InstallsOnlyAfterExplicitConfirm()
    {
        var vm = new InstallViewModel(new TestPromptService());
        var zipPath = Path.Combine(_tempRoot, "ExplicitConfirmPlugin.zip");
        var installedPath = Path.Combine(AppConfig.Instance.GtaPath, "plugins", "LSPDFR", "ExplicitConfirmPlugin.dll");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntry("plugins/LSPDFR/ExplicitConfirmPlugin.dll").Open().Close();

        await ModDownloadBridge.Instance.StageDownloadAsync(zipPath, "ExplicitConfirmPlugin.zip");
        Assert.False(File.Exists(installedPath));
        Assert.Empty(ModLibraryService.Instance.Mods);

        await InvokePrivateTask(vm, "ExecuteInstallCommandAsync");
        Assert.NotNull(vm.ReviewPlan);
        Assert.False(File.Exists(installedPath));
        Assert.Empty(ModLibraryService.Instance.Mods);

        await InvokePrivateTask(vm, "ConfirmInstallAsync");
        await WaitForAsync(() => File.Exists(installedPath));

        Assert.True(File.Exists(installedPath));
        Assert.NotEmpty(ModLibraryService.Instance.Mods);
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

    [Fact]
    public void BrowseCommand_WhenDialogCancelled_DoesNotSetError()
    {
        var vm = new InstallViewModel(new TestPromptService
        {
            NextSelectResult = false
        });

        vm.BrowseCommand.Execute(null);

        Assert.False(vm.HasLastError);
    }

    [Fact]
    public async Task ConfirmInstallAsync_WithMissingSource_ShowsValidationMessage()
    {
        var vm = new InstallViewModel(new TestPromptService());
        vm.DetectedMod = new ModInfo
        {
            Name = "Broken Mod",
            SourcePath = Path.Combine(_tempRoot, "does_not_exist.zip"),
            Files = ["plugins/lspdfr/test.dll"]
        };

        var reviewPlanProp = typeof(InstallViewModel).GetProperty(nameof(InstallViewModel.ReviewPlan));
        Assert.NotNull(reviewPlanProp);
        reviewPlanProp!.SetValue(vm, new InstallPlan());

        var method = typeof(InstallViewModel).GetMethod("ConfirmInstallAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(vm, null)!;
        await task;

        Assert.True(vm.HasLastError);
        Assert.Contains("not found", vm.LastErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InvokePrivateTask(InstallViewModel vm, string methodName)
    {
        var method = typeof(InstallViewModel).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        await (Task)method!.Invoke(vm, null)!;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        Assert.True(condition());
    }
}
