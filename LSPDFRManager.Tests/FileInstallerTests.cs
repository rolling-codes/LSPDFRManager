using System.IO.Compression;
using LSPDFRManager.Models;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="FileInstaller.InstallAsync"/> extraction, rollback, and safety.
/// Validates path traversal protection, partial install tracking, and failure recovery.
/// </summary>
public class FileInstallerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"installer_{Guid.NewGuid():N}");
    private readonly string _tempSource = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");

    public FileInstallerTests()
    {
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_tempSource);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
            if (Directory.Exists(_tempSource))
                Directory.Delete(_tempSource, recursive: true);
        }
        catch { /* cleanup best-effort */ }
    }

    // ── Directory source installs ──────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_CopyDirectory_SucceedsAndTracksFiles()
    {
        var source = Path.Combine(_tempSource, "clean_mod");
        Directory.CreateDirectory(Path.Combine(source, "plugins", "lspdfr"));
        File.WriteAllText(Path.Combine(source, "plugins", "lspdfr", "callout.dll"), "dll content");
        File.WriteAllText(Path.Combine(source, "readme.txt"), "readme");

        var mod = new ModInfo { Name = "Test Mod", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "plugins", "lspdfr", "callout.dll")));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "readme.txt")));
    }

    [Fact]
    public async Task InstallAsync_DirectoryWithNestedStructure_CreatesAllDirectories()
    {
        var source = Path.Combine(_tempSource, "nested_mod");
        var deepPath = Path.Combine(source, "a", "b", "c", "d", "e");
        Directory.CreateDirectory(deepPath);
        File.WriteAllText(Path.Combine(deepPath, "file.dll"), "content");

        var mod = new ModInfo { Name = "Nested Mod", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "a", "b", "c", "d", "e", "file.dll")));
    }

    // ── ZIP archive installs ───────────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_ZipArchive_ExtractsAllFiles()
    {
        var zipPath = Path.Combine(_tempSource, "archive.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("plugin.dll").Open().Close();
            zip.CreateEntry("subfolder/config.ini").Open().Close();
        }

        var mod = new ModInfo { Name = "ZIP Mod", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "plugin.dll")));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "subfolder", "config.ini")));
    }

    [Fact]
    public async Task InstallAsync_ZipWithDirectoryEntries_IgnoresFolders()
    {
        var zipPath = Path.Combine(_tempSource, "with_dirs.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("folder/");  // directory entry
            zip.CreateEntry("folder/file.txt").Open().Close();
        }

        var mod = new ModInfo { Name = "ZIP with dirs", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesWritten);  // only file.txt, not folder/
        Assert.True(File.Exists(Path.Combine(_tempRoot, "folder", "file.txt")));
    }

    // ── Path traversal protection ──────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_ZipWithTraversalPath_FailsAndRollsBack()
    {
        var zipPath = Path.Combine(_tempSource, "malicious.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("legitimate.dll").Open().Close();
            zip.CreateEntry("../../escape.dll").Open().Close();  // traversal attack
        }

        var mod = new ModInfo { Name = "Malicious ZIP", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.Equal(1, result.FilesWritten);  // legitimate.dll extracted before traversal blocked
        Assert.Contains("Path traversal detected", result.Error ?? "");

        // Rollback should have removed the legitimate file
        Assert.False(File.Exists(Path.Combine(_tempRoot, "legitimate.dll")));

        // Escape file never created
        var escapeFile = Path.Combine(Path.GetDirectoryName(_tempRoot)!, "escape.dll");
        Assert.False(File.Exists(escapeFile));
    }

    [Fact]
    public async Task InstallAsync_MultipleTraversalAttempts_RollsBackAll()
    {
        var zipPath = Path.Combine(_tempSource, "multi_attack.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("safe1.dll").Open().Close();
            zip.CreateEntry("safe2.dll").Open().Close();
            zip.CreateEntry("../../../outside.dll").Open().Close();
        }

        var mod = new ModInfo { Name = "Multi-attack ZIP", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.False(File.Exists(Path.Combine(_tempRoot, "safe1.dll")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "safe2.dll")));
    }

    // ── Rollback on failure ────────────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_DirectoryDoesNotExist_Fails()
    {
        var mod = new ModInfo { Name = "Missing", SourcePath = "/nonexistent/path" };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task InstallAsync_CorruptZip_FailsCleanly()
    {
        var zipPath = Path.Combine(_tempSource, "corrupt.zip");
        File.WriteAllText(zipPath, "This is not a valid ZIP file");

        var mod = new ModInfo { Name = "Corrupt ZIP", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task InstallAsync_PartialInstallTracking_CountsWrittenFiles()
    {
        var source = Path.Combine(_tempSource, "partial_test");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "file1.dll"), "1");
        File.WriteAllText(Path.Combine(source, "file2.dll"), "2");

        var mod = new ModInfo { Name = "Partial Test", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.FilesWritten);
    }

    // ── File overwrite behavior ────────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_ExistingFileOverwritten()
    {
        var source = Path.Combine(_tempSource, "overwrite_test");
        Directory.CreateDirectory(source);

        var targetFile = Path.Combine(_tempRoot, "config.ini");
        File.WriteAllText(targetFile, "old content");

        File.WriteAllText(Path.Combine(source, "config.ini"), "new content");

        var mod = new ModInfo { Name = "Overwrite", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        var content = File.ReadAllText(targetFile);
        Assert.Equal("new content", content);
    }

    // ── Large file handling ────────────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_LargeFile_SucceedsWithoutMemoryIssue()
    {
        var source = Path.Combine(_tempSource, "large_mod");
        Directory.CreateDirectory(source);

        // Create a ~5 MB file
        var largeFile = Path.Combine(source, "large.dll");
        using (var fs = File.Create(largeFile))
        {
            fs.SetLength(5 * 1024 * 1024);
        }

        var mod = new ModInfo { Name = "Large Mod", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        var installed = File.Exists(Path.Combine(_tempRoot, "large.dll"));
        Assert.True(installed);
    }

    // ── Empty source handling ──────────────────────────────────────────────

    [Fact]
    public async Task InstallAsync_EmptyDirectory_SucceedsWithZeroFiles()
    {
        var source = Path.Combine(_tempSource, "empty");
        Directory.CreateDirectory(source);

        var mod = new ModInfo { Name = "Empty", SourcePath = source };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesWritten);
    }
}
