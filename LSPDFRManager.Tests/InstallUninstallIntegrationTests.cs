using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// End-to-end integration tests for install, uninstall, and enable/disable flows.
/// Uses real temp directories and real file operations — no mocks.
/// </summary>
[Collection("CommandCenter")]
public class InstallUninstallIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_e2e_{Guid.NewGuid():N}");
    private readonly string _gtaPath;
    private readonly string _archivesPath;
    private readonly InstalledModFileService _fileSvc = new();
    private readonly bool _originalDeleteTempAfterInstall;
    private readonly bool _originalAutoBackupOnInstall;
    private readonly string _originalGtaPath;

    public InstallUninstallIntegrationTests()
    {
        _originalDeleteTempAfterInstall = AppConfig.Instance.DeleteTempAfterInstall;
        _originalAutoBackupOnInstall = AppConfig.Instance.AutoBackupOnInstall;
        _originalGtaPath = AppConfig.Instance.GtaPath;

        // Arrange — isolated temp tree
        _gtaPath = Path.Combine(_tempRoot, "GTA");
        _archivesPath = Path.Combine(_tempRoot, "archives");
        Directory.CreateDirectory(_gtaPath);
        Directory.CreateDirectory(_archivesPath);

        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        AppConfig.Instance.GtaPath = _gtaPath;
        AppConfig.Instance.DeleteTempAfterInstall = true;
        AppConfig.Instance.AutoBackupOnInstall = false;
    }

    public void Dispose()
    {
        AppConfig.Instance.DeleteTempAfterInstall = _originalDeleteTempAfterInstall;
        AppConfig.Instance.AutoBackupOnInstall = _originalAutoBackupOnInstall;
        AppConfig.Instance.GtaPath = _originalGtaPath;
        AppDataPaths.ClearOverride();
        ModLibraryService.Instance.Mods.Clear();
        if (Directory.Exists(_tempRoot))
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private string MakeZip(string name, params string[] entryPaths)
    {
        var zipPath = Path.Combine(_archivesPath, name + ".zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var entry in entryPaths)
        {
            var e = zip.CreateEntry(entry);
            using var s = e.Open();
            s.Write("placeholder"u8);
        }
        return zipPath;
    }

    // ── tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Install_ThenUninstall_LeavesNoFiles()
    {
        // Arrange
        var zipPath = MakeZip("scripthook_roundtrip", "ScriptHookV.asi");
        var mod = new ModInfo { Name = "ScriptHookV", Type = ModType.AsiMod, SourcePath = zipPath };

        // Act — install
        var result = await LSPDFRManager.Services.FileInstaller.InstallAsync(mod, _gtaPath);

        // Assert — files installed
        Assert.True(result.Success, $"Install failed: {result.Error}");
        Assert.NotEmpty(result.WrittenFiles);
        Assert.All(result.WrittenFiles, f => Assert.True(File.Exists(f), $"Expected file on disk: {f}"));

        // Act — uninstall via InstalledModFileService
        var installed = new InstalledMod
        {
            Id = Guid.NewGuid(),
            Name = mod.Name,
            IsEnabled = true,
            InstalledFiles = result.WrittenFiles,
        };
        _fileSvc.Uninstall(installed);

        // Assert — all files removed
        Assert.All(result.WrittenFiles, f => Assert.False(File.Exists(f), $"File should be gone: {f}"));
    }

    [Fact]
    public async Task Install_Disable_Enable_RoundTrip()
    {
        // Arrange
        var zipPath = MakeZip("toggle_test", "ScriptHookV.asi");
        var mod = new ModInfo { Name = "ToggleMod", Type = ModType.AsiMod, SourcePath = zipPath };

        // Act — install
        var result = await LSPDFRManager.Services.FileInstaller.InstallAsync(mod, _gtaPath);
        Assert.True(result.Success, $"Install failed: {result.Error}");

        var installed = new InstalledMod
        {
            Id = Guid.NewGuid(),
            Name = mod.Name,
            IsEnabled = true,
            InstalledFiles = result.WrittenFiles,
        };

        // Act — disable
        _fileSvc.SetEnabled(installed, false);

        // Assert — disabled suffix added, original gone
        Assert.All(result.WrittenFiles, f =>
        {
            Assert.False(File.Exists(f), $"Original file should be gone after disable: {f}");
            Assert.True(File.Exists(f + ".disabled"), $"Expected .disabled file: {f}.disabled");
        });
        Assert.False(installed.IsEnabled);

        // Act — re-enable
        _fileSvc.SetEnabled(installed, true);

        // Assert — original names restored, no .disabled suffix
        Assert.All(result.WrittenFiles, f =>
        {
            Assert.True(File.Exists(f), $"Expected file restored: {f}");
            Assert.False(File.Exists(f + ".disabled"), $"No .disabled file should remain: {f}.disabled");
        });
        Assert.True(installed.IsEnabled);
    }

    [Fact]
    public async Task Install_HappyPath_WrittenFilesMatchDisk()
    {
        // Arrange
        var zipPath = MakeZip("happy_path", "plugins/lspdfr/MyCallout.dll", "plugins/lspdfr/MyCallout.ini");
        var mod = new ModInfo { Name = "MyCallout", Type = ModType.LspdfrPlugin, SourcePath = zipPath };

        // Act
        var result = await LSPDFRManager.Services.FileInstaller.InstallAsync(mod, _gtaPath);

        // Assert
        Assert.True(result.Success, $"Install failed: {result.Error}");
        Assert.Equal(2, result.FilesWritten);
        Assert.All(result.WrittenFiles, f => Assert.True(File.Exists(f)));
    }

    [Fact]
    public async Task Install_ThenUninstall_DeletesRecordedRootFiles()
    {
        var zipPath = MakeZip("root_files", "RootPlugin.asi", "RootPlugin.ini", "plugins/lspdfr/RootCallout.dll");
        var mod = new ModInfo { Name = "Root Files", Type = ModType.AsiMod, SourcePath = zipPath };

        var result = await LSPDFRManager.Services.FileInstaller.InstallAsync(mod, _gtaPath);

        Assert.True(result.Success, $"Install failed: {result.Error}");
        var rootAsi = Path.Combine(_gtaPath, "RootPlugin.asi");
        var rootIni = Path.Combine(_gtaPath, "RootPlugin.ini");
        Assert.Contains(rootAsi, result.WrittenFiles);
        Assert.Contains(rootIni, result.WrittenFiles);
        Assert.True(File.Exists(rootAsi));
        Assert.True(File.Exists(rootIni));

        var installed = new InstalledMod
        {
            Id = Guid.NewGuid(),
            Name = mod.Name,
            IsEnabled = true,
            InstallPath = _gtaPath,
            InstalledFiles = result.WrittenFiles,
        };
        var uninstall = _fileSvc.Uninstall(installed);

        Assert.True(uninstall.Success, uninstall.StatusMessage);
        Assert.All(result.WrittenFiles, f => Assert.False(File.Exists(f), $"File should be gone: {f}"));
    }

    [Fact]
    public async Task QueueInstall_DeletesTempDownloadedArchive_AfterSuccessfulInstall()
    {
        var tempDownloadDir = Path.Combine(Path.GetTempPath(), "LSPDFRManager_downloads");
        Directory.CreateDirectory(tempDownloadDir);
        var zipPath = Path.Combine(tempDownloadDir, $"queue_temp_{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntry("TempRoot.asi").Open().Close();

        var result = await InstallQueue.Instance.EnqueueAsync(new ModInfo
        {
            Name = "Temp Download",
            Type = ModType.AsiMod,
            SourcePath = zipPath,
        });

        Assert.True(result.Success, $"Install failed: {result.Error}");
        Assert.False(File.Exists(zipPath), "Temp downloaded source archive should be deleted after successful install.");
    }

    [Fact]
    public async Task QueueInstall_PreservesUserSelectedArchiveOutsideTemp_AfterSuccessfulInstall()
    {
        var zipPath = MakeZip("user_selected", "UserRoot.asi");

        var result = await InstallQueue.Instance.EnqueueAsync(new ModInfo
        {
            Name = "User Selected",
            Type = ModType.AsiMod,
            SourcePath = zipPath,
        });

        Assert.True(result.Success, $"Install failed: {result.Error}");
        Assert.True(File.Exists(zipPath), "User-selected source archive outside the temp download folder should be preserved.");
    }

    [Fact]
    public async Task QueueInstall_PreservesTempDownloadedArchive_WhenInstallFails()
    {
        var tempDownloadDir = Path.Combine(Path.GetTempPath(), "LSPDFRManager_downloads");
        Directory.CreateDirectory(tempDownloadDir);
        var zipPath = Path.Combine(tempDownloadDir, $"queue_invalid_{Guid.NewGuid():N}.zip");
        File.WriteAllText(zipPath, "not a zip");

        var result = await InstallQueue.Instance.EnqueueAsync(new ModInfo
        {
            Name = "Invalid Temp Download",
            Type = ModType.AsiMod,
            SourcePath = zipPath,
        });

        Assert.False(result.Success);
        Assert.True(File.Exists(zipPath), "Temp downloaded source archive should be preserved when install fails.");
    }
}
