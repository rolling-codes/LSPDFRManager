using LSPDFRManager.Core.Commands;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Install;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using System.Windows;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("AppData serial")]
public class ControlLayerTests : IDisposable
{
    private sealed class TestPromptService : IUserPromptService
    {
        public bool TrySelectModArchive(out string fileName)
        {
            fileName = string.Empty;
            return false;
        }

        public MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
            MessageBoxResult.Yes;
    }

    private sealed class FakeInstallController : IInstallController
    {
        public bool DetectCalled { get; private set; }
        public bool BuildReviewCalled { get; private set; }

        public string FeatureKey => "Install";
        public IReadOnlyDictionary<string, IAppCommand> Commands { get; } = new Dictionary<string, IAppCommand>();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ModInfo> DetectAsync(
            string path,
            string? nameOverride = null,
            string? authorOverride = null,
            CancellationToken cancellationToken = default)
        {
            DetectCalled = true;
            return Task.FromResult(new ModInfo
            {
                Name = string.IsNullOrWhiteSpace(nameOverride) ? "Detected" : nameOverride,
                Author = string.IsNullOrWhiteSpace(authorOverride) ? null : authorOverride,
                SourcePath = path,
                Type = ModType.AsiMod,
                TypeLabel = "ASI Mod",
                Files = ["Plugin.asi"],
                Confidence = 0.9f,
            });
        }

        public Task<IReadOnlyList<ModInfo>> DetectBatchAsync(
            IEnumerable<string> paths,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModInfo>>([]);

        public Task<ModInfo> StageBrowseDownloadAsync(
            string localPath,
            string displayName,
            CancellationToken cancellationToken = default) =>
            DetectAsync(localPath, displayName, cancellationToken: cancellationToken);

        public Task<InstallPlan> BuildReviewPlanAsync(ModInfo mod, CancellationToken cancellationToken = default)
        {
            BuildReviewCalled = true;
            return Task.FromResult(new InstallPlan
            {
                ArchiveSource = mod.SourcePath,
                DetectedType = mod.Type,
                Confidence = mod.Confidence,
            });
        }

        public Task<ConfirmedInstall> ConfirmInstallAsync(
            ModInfo mod,
            string gtaPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfirmedInstall(false, null, gtaPath));
    }

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_control_{Guid.NewGuid():N}");

    public ControlLayerTests()
    {
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        AppConfig.Instance.GtaPath = Path.Combine(_tempRoot, "GTA");
        Directory.CreateDirectory(AppConfig.Instance.GtaPath);
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task AsyncAppCommand_DisablesWhileRunning()
    {
        var release = new TaskCompletionSource();
        var command = new AsyncAppCommand(async (_, _) => await release.Task);

        var running = command.ExecuteAsync();
        await WaitForAsync(() => !command.CanExecute());

        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute());

        release.SetResult();
        await running;

        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute());
    }

    [Fact]
    public async Task InstallViewModel_DelegatesDetectionAndReviewToController()
    {
        var controller = new FakeInstallController();
        var vm = new InstallViewModel(new TestPromptService(), controller)
        {
            NameOverride = "Override Name"
        };
        var archivePath = Path.Combine(_tempRoot, "mod.zip");
        File.WriteAllText(archivePath, "placeholder");

        await vm.DetectAsync(archivePath);
        await InvokePrivateTask(vm, "ExecuteInstallCommandAsync");

        Assert.True(controller.DetectCalled);
        Assert.True(controller.BuildReviewCalled);
        Assert.Equal("Override Name", vm.DetectedMod!.Name);
        Assert.NotNull(vm.ReviewPlan);
    }

    [Fact]
    public async Task BrowseViewModel_Dispose_UnsubscribesFromDownloadBridge()
    {
        var vm = new BrowseViewModel();
        vm.Dispose();
        var zipPath = MakeZip("DisposedBrowseVm", "plugins/LSPDFR/DisposedBrowseVm.dll");

        await ModDownloadBridge.Instance.StageDownloadAsync(zipPath, "DisposedBrowseVm.zip");

        Assert.Equal("Ready", vm.StatusMessage);
    }

    [Fact]
    public async Task InstallViewModel_Dispose_UnsubscribesFromDownloadBridge()
    {
        var vm = new InstallViewModel(new TestPromptService());
        vm.Dispose();
        var zipPath = MakeZip("DisposedInstallVm", "plugins/LSPDFR/DisposedInstallVm.dll");

        await ModDownloadBridge.Instance.StageDownloadAsync(zipPath, "DisposedInstallVm.zip");

        Assert.Null(vm.DetectedMod);
        Assert.Empty(vm.Log);
    }

    private string MakeZip(string name, params string[] entries)
    {
        var path = Path.Combine(_tempRoot, name + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
            zip.CreateEntry(entry).Open().Close();
        return path;
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

            await Task.Delay(25);
        }

        Assert.True(condition());
    }
}
