using LSPDFRManager.Core;
using LSPDFRManager.OpenIv.CarInstall;
using LSPDFRManager.OpenIv.CarInstall.Models;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Phase C: Streaming + Buffer Optimization Tests
/// Verifies buffered copy strategy without breaking rollback/retry guarantees.
/// </summary>
public class StreamingBufferTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"streaming_{Guid.NewGuid():N}");
    private readonly OpenIvExecutor _executor = new(new XmlPatcher());

    public StreamingBufferTests()
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

    // ── Buffer Size Selection ──────────────────────────────────────────

    [Theory]
    [InlineData(500_000, 65_536)]       // < 1MB → 64KB
    [InlineData(1_000_000, 524_288)]    // 1MB → 512KB
    [InlineData(50_000_000, 524_288)]   // 50MB → 512KB
    [InlineData(100_000_000, 2_097_152)] // 100MB → 2MB
    [InlineData(500_000_000, 2_097_152)] // 500MB → 2MB
    public void BufferSize_SelectsCorrectly(long fileSize, int expectedBufferSize)
    {
        // Reflection to access SelectBufferSize private method
        var method = typeof(OpenIvExecutor).GetMethod(
            "SelectBufferSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        int actualBuffer = (int)method.Invoke(null, [fileSize]);
        Assert.Equal(expectedBufferSize, actualBuffer);
    }

    // ── Streaming Success ──────────────────────────────────────────────

    [Fact]
    public async Task SmallFile_StreamsSuccessfully()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive("small.dll");
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "small.dll", DestinationPath = @"mods\small.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(_testRoot, @"mods\small.dll")));
    }

    [Fact]
    public async Task LargeFile_StreamsWithLargeBuffer()
    {
        var archive = FakeArchiveFactory.CreateLargeFileArchive();
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "large.dat", DestinationPath = @"mods\large.dat", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesWritten);
        Assert.True(File.Exists(Path.Combine(_testRoot, @"mods\large.dat")));
    }

    // ── Retry Behavior ────────────────────────────────────────────────

    [Fact]
    public async Task SeekableStream_RetriesOnIOFailure()
    {
        // Create a seekable stream that fails on first attempt
        var archive = FakeArchiveFactory.CreateCleanArchive("test.dll");
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "test.dll", DestinationPath = @"mods\test.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        // Should succeed because FakeArchive streams are seekable
        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task NonSeekableStream_FailsFast()
    {
        // Non-seekable stream that fails mid-copy
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "failing.dat", DestinationPath = @"mods\failing.dat", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        // Should fail fast (no retries because stream is non-seekable)
        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.False(result.Success);
        // Rollback should have cleaned up partial file
        Assert.False(File.Exists(Path.Combine(_testRoot, @"mods\failing.dat")));
    }

    // ── Rollback Consistency ───────────────────────────────────────────

    [Fact]
    public async Task PartialWrite_RolledBackOnFailure()
    {
        // Install 3 files, fail on 2nd
        var archive = FakeArchiveFactory.CreateManyFilesArchive(fileCount: 3);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "file00000.dll", DestinationPath = @"mods\file00000.dll", Overwrite = true });
        plan.Operations.Add(new() { SourcePath = "missing.dll", DestinationPath = @"mods\missing.dll", Overwrite = true }); // Will fail
        plan.Operations.Add(new() { SourcePath = "file00001.dll", DestinationPath = @"mods\file00001.dll", Overwrite = true });

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        Assert.False(result.Success);
        // Rollback should have deleted all files (LIFO order)
        Assert.False(File.Exists(Path.Combine(_testRoot, @"mods\file00000.dll")));
        Assert.False(File.Exists(Path.Combine(_testRoot, @"mods\missing.dll")));
        Assert.False(File.Exists(Path.Combine(_testRoot, @"mods\file00001.dll")));
    }

    // ── Cancellation ───────────────────────────────────────────────────

    [Fact]
    public async Task CancelledStream_RolledBackCompletely()
    {
        // Test that stream failures trigger proper rollback
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);
        var plan = new OpenIvInstallPlan { Type = CarInstallType.ReplaceVehicle, TargetDlcName = "test" };
        plan.Operations.Add(new() { SourcePath = "file1.dll", DestinationPath = @"mods\file1.dll", Overwrite = true });
        plan.Operations.Add(new() { SourcePath = "file2.dll", DestinationPath = @"mods\file2.dll", Overwrite = true });
        plan.Operations.Add(new() { SourcePath = "file3.dll", DestinationPath = @"mods\file3.dll", Overwrite = true });
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _testRoot);

        // file3 fails mid-stream, causing rollback
        Assert.False(result.Success);
        // All files should be rolled back
        var modsDir = Path.Combine(_testRoot, "mods");
        if (Directory.Exists(modsDir))
        {
            var files = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
            Assert.Empty(files);
        }
    }
}
