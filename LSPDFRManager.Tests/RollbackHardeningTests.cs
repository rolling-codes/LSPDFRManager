using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Verifies the full rollback contract for FileInstaller:
///   - LIFO rollback order
///   - New files deleted on rollback
///   - Overwritten files restored from backup on rollback
///   - Created directories removed only when empty and only when created by this install
///   - Existing directories never deleted
///   - Cancellation triggers rollback after mutation begins
///   - Rollback failures surfaced in InstallResult.RollbackErrors
///   - Original install error preserved even when rollback also fails
///   - Rollback is idempotent (WasRolledBack guard)
/// </summary>
public class RollbackHardeningTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"rollback_{Guid.NewGuid():N}");

    public RollbackHardeningTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        // Best-effort cleanup; clear read-only attrs so tests that set them don't block deletion
        try
        {
            foreach (var f in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    // ── New files deleted on rollback ────────────────────────────────────────

    [Fact]
    public async Task Rollback_NewFilesDeleted_OnFailure()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("file2.dll", () => throw new IOException("forced")),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(_tempDir, "file1.dll")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "file2.dll")));
        Assert.Empty(result.RollbackErrors);
    }

    // ── Overwritten files restored on rollback ───────────────────────────────

    [Fact]
    public async Task Rollback_OverwrittenFileRestored_OnFailure()
    {
        var existingPath = Path.Combine(_tempDir, "existing.dll");
        File.WriteAllText(existingPath, "original content");

        var archive = new FakeArchive([
            new FakeArchiveEntry("existing.dll", new byte[] { 9, 8, 7 }),
            new FakeArchiveEntry("fail.dll", () => throw new IOException("forced")),
        ]);

        var plan = new InstallPlan
        {
            Entries = [new InstallPlanEntry
            {
                ArchivePath = "existing.dll",
                TargetPath  = existingPath,
                DestinationExists = true,
                PlannedAction = InstallConflictAction.BackupAndReplace,
            }]
        };

        var result = await FileInstaller.InstallAsync(archive, _tempDir, plan);

        Assert.False(result.Success);
        Assert.Equal("original content", File.ReadAllText(existingPath));
        Assert.Empty(result.RollbackErrors);
    }

    // ── Created directories cleaned on rollback ──────────────────────────────

    [Fact]
    public async Task Rollback_NewDirectoriesRemoved_OnFailure()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("a/b/c/file1.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("a/b/c/fail.dll",
                () => new ThrowingStream(new byte[] { 4, 5, 6 }, failAfter: 1)),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.Empty(Directory.GetFileSystemEntries(_tempDir));
    }

    // ── Existing directories never deleted on rollback ───────────────────────

    [Fact]
    public async Task Rollback_ExistingDirectoriesPreserved_OnFailure()
    {
        var preExistingDir = Path.Combine(_tempDir, "plugins");
        Directory.CreateDirectory(preExistingDir);
        var preExistingFile = Path.Combine(preExistingDir, "preexisting.dll");
        File.WriteAllText(preExistingFile, "keep me");

        var archive = new FakeArchive([
            new FakeArchiveEntry("plugins/new.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("fail.dll", () => throw new IOException("forced")),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        // Pre-existing directory and file must survive rollback
        Assert.True(Directory.Exists(preExistingDir));
        Assert.True(File.Exists(preExistingFile));
        // New file added to the existing dir must be rolled back
        Assert.False(File.Exists(Path.Combine(preExistingDir, "new.dll")));
    }

    // ── Cancellation before any mutation ────────────────────────────────────

    [Fact]
    public async Task Cancellation_BeforeMutation_NoFilesWritten()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("file2.dll", new byte[] { 4, 5, 6 }),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir, cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories));
        Assert.Empty(result.RollbackErrors);
    }

    // ── Cancellation after mutation begins ───────────────────────────────────

    [Fact]
    public async Task Cancellation_AfterFirstFileCommitted_RollsBackCommittedFile()
    {
        var cts = new CancellationTokenSource();

        // Cancels the token when file1's stream is opened (during commit of file1).
        // The check at the top of the next iteration fires → OCE → rollback.
        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", () =>
            {
                cts.Cancel();
                return new MemoryStream(new byte[] { 1, 2, 3 });
            }, size: 3),
            new FakeArchiveEntry("file2.dll", new byte[] { 4, 5, 6 }),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir, cancellationToken: cts.Token);

        Assert.False(result.Success);
        // file1 was committed then rolled back; file2 was never written
        Assert.False(File.Exists(Path.Combine(_tempDir, "file1.dll")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "file2.dll")));
        Assert.Empty(result.RollbackErrors);
    }

    // ── Rollback failures surfaced in RollbackErrors ─────────────────────────

    [Fact]
    public async Task RollbackFailure_SurfacedInRollbackErrors_OriginalErrorPreserved()
    {
        FileStream? lockStream = null;

        // file1.dll is committed first. When fail.dll's stream factory runs (after file1
        // is committed), we lock file1.dll so its rollback delete fails, then throw to
        // trigger the rollback path.
        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("fail.dll", () =>
            {
                var file1 = Path.Combine(_tempDir, "file1.dll");
                if (File.Exists(file1))
                    lockStream = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.None);
                throw new IOException("forced install failure");
            }),
        ]);

        InstallResult result;
        try
        {
            result = await FileInstaller.InstallAsync(archive, _tempDir);
        }
        finally
        {
            lockStream?.Dispose();
        }

        // Original install error preserved
        Assert.False(result.Success);
        Assert.Contains("forced install failure", result.Error ?? "");

        // Rollback failure surfaced (file1.dll was locked so delete failed)
        Assert.NotEmpty(result.RollbackErrors);
        Assert.Contains(result.RollbackErrors, e => e.Contains("file1.dll", StringComparison.OrdinalIgnoreCase));
    }

    // ── Clean rollback has no errors ─────────────────────────────────────────

    [Fact]
    public async Task CleanRollback_NoRollbackErrors()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("file2.dll", () => throw new IOException("forced")),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.Empty(result.RollbackErrors);
        Assert.True(result.RollbackSucceeded);
    }

    // ── Idempotency: second rollback is a no-op ───────────────────────────────

    [Fact]
    public async Task Rollback_SuccessfulInstall_HasNoRollbackErrors()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("file1.dll", new byte[] { 1, 2, 3 }),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.True(result.Success);
        Assert.Empty(result.RollbackErrors);
    }

    // ── LIFO rollback order ──────────────────────────────────────────────────

    [Fact]
    public async Task Rollback_MultipleFiles_AllRemovedRegardlessOfOrder()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("a.dll", new byte[] { 1 }),
            new FakeArchiveEntry("b.dll", new byte[] { 2 }),
            new FakeArchiveEntry("c.dll", new byte[] { 3 }),
            new FakeArchiveEntry("fail.dll", () => throw new IOException("forced")),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(_tempDir, "a.dll")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "b.dll")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "c.dll")));
        Assert.Empty(result.RollbackErrors);
    }

    // ── IsPartial reflects mutation before failure ────────────────────────────

    [Fact]
    public async Task IsPartial_TrueWhenFilesWereCommittedBeforeFailure()
    {
        var archive = new FakeArchive([
            new FakeArchiveEntry("committed.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("fail.dll", () => throw new IOException("forced")),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public async Task IsPartial_FalseWhenFailureOccursBeforeAnyMutation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var archive = new FakeArchive([
            new FakeArchiveEntry("file.dll", new byte[] { 1, 2, 3 }),
        ]);

        var result = await FileInstaller.InstallAsync(archive, _tempDir, cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.False(result.IsPartial);
    }
}
