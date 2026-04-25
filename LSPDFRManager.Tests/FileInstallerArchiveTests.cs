using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Unit tests for FileInstaller using IArchive and fake implementations.
/// Tests rollback guarantees, safety invariants, and corruption handling.
/// </summary>
public class FileInstallerArchiveTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"archive_tests_{Guid.NewGuid():N}");

    public FileInstallerArchiveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* cleanup best-effort */ }
    }

    // ── Path Traversal Safety ──────────────────────────────────────────────

    [Fact]
    public async Task Install_PathTraversal_FailsAndRollsBack()
    {
        var archive = FakeArchiveFactory.CreatePathTraversalArchive();

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.Contains("traversal", result.Error ?? "", StringComparison.OrdinalIgnoreCase);

        // legitimate.dll should be rolled back
        Assert.False(File.Exists(Path.Combine(_tempDir, "legitimate.dll")));

        // escape.exe never written
        var parentDir = Path.GetDirectoryName(_tempDir)!;
        Assert.False(File.Exists(Path.Combine(parentDir, "escape.exe")));
    }

    // ── Rollback Guarantees ────────────────────────────────────────────────

    [Fact]
    public async Task Install_MidStreamFailure_RollsBackAllFiles()
    {
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);

        // All files should be removed on failure
        var files = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories);
        Assert.Empty(files);
    }

    [Fact]
    public async Task Install_StreamOpenFailure_RollsBackPreviousFiles()
    {
        var archive = FakeArchiveFactory.CreateStreamOpenFailureArchive();

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);

        // safe.dll should be rolled back
        Assert.False(File.Exists(Path.Combine(_tempDir, "safe.dll")));
    }

    // ── Deep Path Handling ─────────────────────────────────────────────────

    [Fact]
    public async Task Install_DeepNestedPath_SucceedsAndCreatesAllDirectories()
    {
        var archive = FakeArchiveFactory.CreateDeepNestedPathArchive();

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        Assert.False(result.IsPartial);
        Assert.Equal(1, result.FilesWritten);

        var expectedPath = Path.Combine(_tempDir, "a", "b", "c", "d", "e", "f", "g", "deep.dll");
        Assert.True(File.Exists(expectedPath));
    }

    // ── Large Files ────────────────────────────────────────────────────────

    [Fact]
    public async Task Install_LargeFile_SucceedsWithoutMemoryIssue()
    {
        var archive = FakeArchiveFactory.CreateLargeFileArchive();

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        var largeFile = Path.Combine(_tempDir, "large.dat");
        Assert.True(File.Exists(largeFile));
        Assert.True(new FileInfo(largeFile).Length > 1_000_000);
    }

    // ── Many Files ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Install_ManyFiles_AllExtracted()
    {
        var archive = FakeArchiveFactory.CreateManyFilesArchive(100);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        Assert.Equal(100, result.FilesWritten);

        var extractedCount = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(100, extractedCount);
    }

    // ── Empty Archive ──────────────────────────────────────────────────────

    [Fact]
    public async Task Install_EmptyArchive_Succeeds()
    {
        var archive = FakeArchiveFactory.CreateEmptyArchive();

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesWritten);
    }

    // ── Clean Archive ──────────────────────────────────────────────────────

    [Fact]
    public async Task Install_CleanArchive_AllFilesExtracted()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "plugin.dll",
            "config/settings.ini",
            "data/script.cs"
        );

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        Assert.False(result.IsPartial);
        Assert.Equal(3, result.FilesWritten);

        Assert.True(File.Exists(Path.Combine(_tempDir, "plugin.dll")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "config", "settings.ini")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "data", "script.cs")));
    }

    // ── File Overwrite ─────────────────────────────────────────────────────

    [Fact]
    public async Task Install_ExistingFileOverwritten()
    {
        var existingFile = Path.Combine(_tempDir, "existing.dll");
        File.WriteAllText(existingFile, "old content");

        var archive = FakeArchiveFactory.CreateCleanArchive("existing.dll");

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        var content = File.ReadAllText(existingFile);
        Assert.NotEqual("old content", content);
    }
}
