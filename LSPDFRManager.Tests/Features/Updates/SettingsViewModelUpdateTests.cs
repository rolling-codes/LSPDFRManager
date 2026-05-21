using System;
using System.Threading;
using System.Threading.Tasks;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Updates;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests.Features.Updates;

[Collection("AppData serial")]
public class SettingsViewModelUpdateTests : IDisposable
{
    private sealed class FakeUpdateController : IUpdateController
    {
        public UpdateCheckResult? ResultToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return Task.FromResult(ResultToReturn!);
        }
    }

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_svm_upd_{Guid.NewGuid():N}");

    public SettingsViewModelUpdateTests()
    {
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task CheckForUpdates_UpdateAvailable_SetsStatusAndProperties()
    {
        var controller = new FakeUpdateController
        {
            ResultToReturn = new UpdateCheckResult
            {
                UpdateAvailable = true,
                LatestVersion = "9.9.9",
                DownloadUrl = "https://example.com/release",
            }
        };
        var vm = new SettingsViewModel(controller);

        await ExecuteCheckForUpdatesAsync(vm);

        Assert.True(vm.UpdateAvailable);
        Assert.Equal("9.9.9", vm.LatestVersion);
        Assert.Equal("https://example.com/release", vm.DownloadUrl);
        Assert.Contains("9.9.9", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CheckForUpdates_NoUpdateAvailable_SetsLatestVersionMessage()
    {
        var controller = new FakeUpdateController
        {
            ResultToReturn = new UpdateCheckResult
            {
                UpdateAvailable = false,
                LatestVersion = "3.7.4",
            }
        };
        var vm = new SettingsViewModel(controller);

        await ExecuteCheckForUpdatesAsync(vm);

        Assert.False(vm.UpdateAvailable);
        Assert.Contains("latest", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CheckForUpdates_ControllerThrows_SetsErrorStatusMessage()
    {
        var controller = new FakeUpdateController
        {
            ExceptionToThrow = new InvalidOperationException("Network failure")
        };
        var vm = new SettingsViewModel(controller);

        await ExecuteCheckForUpdatesAsync(vm);

        Assert.Contains("Network failure", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }

    private static async Task ExecuteCheckForUpdatesAsync(SettingsViewModel vm)
    {
        var method = typeof(SettingsViewModel).GetMethod(
            "ExecuteCheckForUpdatesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        await (Task)method!.Invoke(vm, null)!;
    }
}
