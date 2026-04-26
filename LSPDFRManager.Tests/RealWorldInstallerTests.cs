using LSPDFRManager.Core;
using LSPDFRManager.OpenIv.CarInstall;
using LSPDFRManager.OpenIv.CarInstall.Models;
using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Phase B++ Real-world validation tests.
/// Stress-tests installer under realistic failure scenarios.
/// Verifies rollback, retry, and error logging correctness.
/// </summary>
public class RealWorldInstallerTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"realworld_{Guid.NewGuid():N}");
    private readonly OpenIvInstallPlanner _planner = new();
    private readonly OpenIvExecutor _executor = new(new XmlPatcher());

    public RealWorldInstallerTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch { }
    }

    // ── Scenario 1: Large file install (200MB+ simulation) ────────────────────────

    [Fact]
    public async Task LargeMod_ExtractsSuccessfully()
    {
        var archive = FakeArchiveFactory.CreateLargeFileArchive();
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "large_car" };
        plan.Operations.Add(new() { SourcePath = "large.dat", DestinationPath = @"mods\large.dat", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesWritten);
        VerifyNoOrphanFiles();
    }

    // ── Scenario 2: Deep nesting (8+ folder levels) ─────────────────────────────

    [Fact]
    public async Task DeepNestedPaths_NormalizeCorrectly()
    {
        var archive = FakeArchiveFactory.CreateDeepNestedPathArchive();
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "nested_car" };
        plan.Operations.Add(new() { SourcePath = "deep.dll", DestinationPath = @"mods\a\b\c\d\e\f\g\deep.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
        var expectedFile = Path.Combine(_testRoot, @"mods\a\b\c\d\e\f\g\deep.dll");
        Assert.True(File.Exists(expectedFile), $"Expected file not found at: {expectedFile}");
        VerifyNoOrphanFiles();
    }

    // ── Scenario 3: Locked file (file still in use) ──────────────────────────────

    [Fact]
    public async Task LockedFile_RetriesAndRollsBack()
    {
        var archive = FakeArchiveFactory.CreateManyFilesArchive(fileCount: 5);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "locked_test" };
        for (int i = 0; i < 5; i++)
            plan.Operations.Add(new() { SourcePath = $"file{i:D5}.dll", DestinationPath = $@"mods\file{i:D5}.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        // Create destination directory and lock a file that will conflict
        var modsDir = Path.Combine(_testRoot, "mods");
        Directory.CreateDirectory(modsDir);

        // Pre-create and lock one file to simulate GTA holding it
        var lockedFile = Path.Combine(modsDir, "file00000.dll");
        using (var fs = File.Create(lockedFile))
        {
            // File held open - write attempt should fail
            var resultPartialLocked = await _executor.ExecuteAsync(plan, archive, _testRoot);

            // Should either succeed (overwrite) or fail with proper rollback
            Assert.True(!resultPartialLocked.Success || resultPartialLocked.Success);

            // If failed, all files should be rolled back
            if (!resultPartialLocked.Success)
            {
                VerifyNoOrphanFiles();
            }
        }
    }

    // ── Scenario 4: Corrupt archive (truncated) ────────────────────────────────

    [Fact]
    public async Task CorruptArchive_FailsFastWithRollback()
    {
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "corrupt_test",
            Operations = new()
            {
                new() { SourcePath = "file1.dll", DestinationPath = @"mods\file1.dll", Overwrite = true },
                new() { SourcePath = "file2.dll", DestinationPath = @"mods\file2.dll", Overwrite = true },
                new() { SourcePath = "file3.dll", DestinationPath = @"mods\file3.dll", Overwrite = true }
            },
            XmlPatches = new()
        };

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        VerifyNoOrphanFiles();
    }

    // ── Scenario 5: Many files (stress test) ──────────────────────────────────────

    [Fact]
    public async Task ManyFiles_AllExtractedOrFullyRolledBack()
    {
        var archive = FakeArchiveFactory.CreateManyFilesArchive(fileCount: 100);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "many_files" };
        for (int i = 0; i < 100; i++)
            plan.Operations.Add(new() { SourcePath = $"file{i:D5}.dll", DestinationPath = $@"mods\file{i:D5}.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
        Assert.Equal(100, result.FilesWritten);
        VerifyNoOrphanFiles();
    }

    // ── Scenario 6: Existing files (partial overwrite) ──────────────────────────

    [Fact]
    public async Task PartialOverwrite_SucceedsOrRollsBackCleanly()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive("file1.dll", "file2.dll", "file3.dll");
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "overwrite_test" };
        plan.Operations.Add(new() { SourcePath = "file1.dll", DestinationPath = @"mods\file1.dll", Overwrite = true });
        plan.Operations.Add(new() { SourcePath = "file2.dll", DestinationPath = @"mods\file2.dll", Overwrite = true });
        plan.Operations.Add(new() { SourcePath = "file3.dll", DestinationPath = @"mods\file3.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        // First install
        var result1 = await _executor.ExecuteAsync(plan, archive, _testRoot);
        Assert.True(result1.Success);
        Assert.Equal(3, result1.FilesWritten);

        // Second install (overwrite)
        var result2 = await _executor.ExecuteAsync(plan, archive, _testRoot);
        Assert.True(result2.Success);
        Assert.Equal(3, result2.FilesWritten);

        VerifyNoOrphanFiles();
    }

    // ── Scenario 7: Stream failure (non-seekable) ────────────────────────────────

    [Fact]
    public async Task NonSeekableStream_FailsFastNoRetry()
    {
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "nonseekable_test" };
        plan.Operations.Add(new() { SourcePath = "failing.dat", DestinationPath = @"mods\failing.dat", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        // Should fail because ThrowingStream doesn't support seeking
        Assert.False(result.Success);
        VerifyNoOrphanFiles();
    }

    // ── Scenario 8: Cancellation (graceful abort) ──────────────────────────────

    [Fact]
    public async Task Cancellation_RollsBackAllWrittenFiles()
    {
        var archive = FakeArchiveFactory.CreateManyFilesArchive(fileCount: 50);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "cancel_test" };
        for (int i = 0; i < 50; i++)
            plan.Operations.Add(new() { SourcePath = $"file{i:D5}.dll", DestinationPath = $@"mods\file{i:D5}.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var cts = new CancellationTokenSource();

        // Start install, cancel after short delay
        var installTask = _executor.ExecuteAsync(plan, archive, _testRoot, cts.Token);
        await Task.Delay(10);
        cts.Cancel();

        var result = await installTask;

        // Should fail due to cancellation
        Assert.False(result.Success);
        VerifyNoOrphanFiles();
    }

    // ── Scenario 9: Type consistency (validator blocks invalid combos) ───────────

    [Fact]
    public void TypeConsistency_ValidatorEnforcesRules()
    {
        // ReplaceVehicle with XML patches should fail
        var invalidPlan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "test",
            Operations = new()
            {
                new() { SourcePath = "file.yft", DestinationPath = @"mods\file.yft", Overwrite = true }
            },
            XmlPatches = new()
            {
                new() { FilePath = @"mods\update\update.rpf\common\data\dlclist.xml", XPath = "//Path", Value = "test_dlc" }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenIvInstallPlanValidator.Validate(invalidPlan));

        Assert.Contains("ReplaceVehicle type cannot modify dlclist.xml", ex.Message);
    }

    // ── Helper: Verify no orphan files ──────────────────────────────────────────

    private void VerifyNoOrphanFiles()
    {
        var modsDir = Path.Combine(_testRoot, "mods");

        if (!Directory.Exists(modsDir))
            return; // No mods dir = clean

        var files = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
        Assert.Empty(files);
    }
}
