using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// End-to-end integration tests for install, uninstall, and enable/disable flows.
/// Uses real temp directories and real file operations — no mocks.
/// </summary>
public class InstallUninstallIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_e2e_{Guid.NewGuid():N}");
    private readonly string _gtaPath;
    private readonly string _archivesPath;
    private readonly InstalledModFileService _fileSvc = new();

    public InstallUninstallIntegrationTests()
    {
        // Arrange — isolated temp tree
        _gtaPath = Path.Combine(_tempRoot, "GTA");
        _archivesPath = Path.Combine(_tempRoot, "archives");
        Directory.CreateDirectory(_gtaPath);
        Directory.CreateDirectory(_archivesPath);

        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
    }

    public void Dispose()
    {
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
}
