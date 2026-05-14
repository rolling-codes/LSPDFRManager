using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("AppData serial")]
public class BackupServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_backup_{Guid.NewGuid():N}");
    private readonly string _origBackupPath;

    public BackupServiceTests()
    {
        // Arrange — redirect all AppData I/O to a temp directory
        Directory.CreateDirectory(_tempRoot);
        var fakeAppData = Path.Combine(_tempRoot, "AppData");
        Directory.CreateDirectory(fakeAppData);
        AppDataPaths.OverrideRoot(fakeAppData);
        AppDataPaths.EnsureRootExists();

        // Write a minimal library.json so the backup has something to include
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");

        // Point BackupPath to a sibling folder in temp
        _origBackupPath = AppConfig.Instance.BackupPath;
        AppConfig.Instance.BackupPath = Path.Combine(_tempRoot, "Backups");
        Directory.CreateDirectory(AppConfig.Instance.BackupPath);
    }

    public void Dispose()
    {
        try { ModLibraryService.Instance.Mods.Clear(); } catch { }
        AppConfig.Instance.BackupPath = _origBackupPath;
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task CreateBackup_CreatesZipFile()
    {
        // Arrange
        var svc = new BackupService();

        // Act
        var zipPath = await svc.CreateBackupAsync();

        // Assert
        Assert.True(File.Exists(zipPath), $"Expected zip at: {zipPath}");
        Assert.EndsWith(".zip", zipPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBackup_ZipContainsLibraryJson()
    {
        // Arrange
        var svc = new BackupService();

        // Act
        var zipPath = await svc.CreateBackupAsync();

        // Assert
        using var zip = ZipFile.OpenRead(zipPath);
        var hasLibrary = zip.Entries.Any(e =>
            e.FullName.EndsWith("library.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasLibrary, "Backup zip should contain library.json");
    }

    [Fact]
    public async Task ListBackups_AfterCreate_ReturnsCreatedBackup()
    {
        // Arrange
        var svc = new BackupService();
        await svc.CreateBackupAsync();

        // Act
        var backups = svc.ListBackups().ToList();

        // Assert
        Assert.Single(backups);
        Assert.All(backups, b => Assert.EndsWith(".zip", b, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RestoreFromBackup_MissingFile_ThrowsException()
    {
        // Arrange
        var svc = new BackupService();
        var nonExistent = Path.Combine(_tempRoot, "does_not_exist.zip");

        // Act + Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.RestoreFromBackupAsync(nonExistent));
        Assert.False(string.IsNullOrEmpty(ex.Message));
    }

    [Fact]
    public async Task RestoreFromBackup_RestoresLibraryJson()
    {
        // Arrange
        var svc = new BackupService();
        var originalContent = """[{"Id":"abc","Name":"TestMod"}]""";
        File.WriteAllText(AppDataPaths.LibraryFile, originalContent);
        var zipPath = await svc.CreateBackupAsync();

        // Wipe the library to simulate a fresh state
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");

        // Act
        await svc.RestoreFromBackupAsync(zipPath);

        // Assert
        var restored = File.ReadAllText(AppDataPaths.LibraryFile);
        Assert.Equal(originalContent, restored);
    }
}
