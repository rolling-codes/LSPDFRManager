using LSPDFRManager.Domain;
using LSPDFRManager.OpenIv.CarInstall;
using LSPDFRManager.OpenIv.CarInstall.Models;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// End-to-end integration tests: full pipeline (Analyzer → Planner → Validator → Executor).
/// Tests with real filesystem (temp directories) and actual file extraction + rollback.
/// </summary>
public class OpenIvExecutorIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"executor_{Guid.NewGuid():N}");
    private readonly OpenIvInstallPlanner _planner = new();
    private readonly IXmlPatcher _xmlPatcher = new XmlPatcher();
    private readonly OpenIvExecutor _executor;

    public OpenIvExecutorIntegrationTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _executor = new OpenIvExecutor(_xmlPatcher);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { }
    }

    // ── Test 1: ReplaceVehicle Full Flow ─────────────────────────────────

    [Fact]
    public async Task ReplaceVehicle_FullPipeline_ExtractsFilesSuccessfully()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = _planner.BuildPlan(CarInstallType.ReplaceVehicle, archive, "test_car");
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesWritten);
        Assert.Null(result.Error);

        var expectedFiles = new[]
        {
            Path.Combine(_tempRoot, @"mods\update\x64\dlcpacks\patchday\car.yft"),
            Path.Combine(_tempRoot, @"mods\update\x64\dlcpacks\patchday\car.ytd")
        };

        foreach (var file in expectedFiles)
            Assert.True(File.Exists(file), $"Expected file not found: {file}");
    }

    // ── Test 2: AddonDLC with XML Patch ──────────────────────────────────

    [Fact]
    public async Task AddonDLC_FullPipeline_ExtractsAndPatchesXml()
    {
        var archive = FakeArchiveFactory.CreateAddonDLC();
        var plan = _planner.BuildPlan(CarInstallType.AddonDLC, archive, "test_dlc");
        OpenIvInstallPlanValidator.Validate(plan);

        SetupDlcListXml(_tempRoot);

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.True(result.Success);
        Assert.True(result.FilesWritten >= 3);

        var dlclistPath = Path.Combine(_tempRoot, @"mods\update\update.rpf\common\data\dlclist.xml");
        var dlclistContent = File.ReadAllText(dlclistPath);
        Assert.Contains("test_dlc", dlclistContent);
    }

    // ── Test 3: ConfigPatch Flow ─────────────────────────────────────────

    [Fact]
    public async Task ConfigPatch_FullPipeline_ExtractsMetaFiles()
    {
        var archive = FakeArchiveFactory.CreateConfigPatch();
        var plan = _planner.BuildPlan(CarInstallType.ConfigPatch, archive, "config_mod");
        OpenIvInstallPlanValidator.Validate(plan);

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesWritten);

        var expectedFiles = new[]
        {
            Path.Combine(_tempRoot, @"mods\handling.meta"),
            Path.Combine(_tempRoot, @"mods\vehicles.meta")
        };

        foreach (var file in expectedFiles)
            Assert.True(File.Exists(file), $"Expected file not found: {file}");
    }

    // ── Test 4: Rollback on Extraction Failure ───────────────────────────

    [Fact]
    public async Task Executor_PartialExtraction_RollsBackAllFiles()
    {
        var archive = FakeArchiveFactory.CreateMidStreamFailureArchive(failAfterBytes: 5);
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "failing_mod",
            Operations = new()
            {
                new() { SourcePath = "file1.dll", DestinationPath = @"mods\file1.dll", Overwrite = true },
                new() { SourcePath = "file2.dll", DestinationPath = @"mods\file2.dll", Overwrite = true },
                new() { SourcePath = "file3.dll", DestinationPath = @"mods\file3.dll", Overwrite = true }
            },
            XmlPatches = new()
        };

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.NotNull(result.Error);

        var file1 = Path.Combine(_tempRoot, @"mods\file1.dll");
        var file2 = Path.Combine(_tempRoot, @"mods\file2.dll");
        var file3 = Path.Combine(_tempRoot, @"mods\file3.dll");

        Assert.False(File.Exists(file1), "file1 should be rolled back");
        Assert.False(File.Exists(file2), "file2 should be rolled back");
        Assert.False(File.Exists(file3), "file3 should be rolled back");
    }

    // ── Test 5: Missing Archive Entry ────────────────────────────────────

    [Fact]
    public async Task Executor_MissingArchiveEntry_FailsGracefully()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "bad_mod",
            Operations = new()
            {
                new() { SourcePath = "nonexistent.yft", DestinationPath = @"mods\nonexistent.yft", Overwrite = true }
            },
            XmlPatches = new()
        };

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Executor_TraversalDestination_FailsWithoutWritingOutsideRoot()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive("handling.meta");
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ConfigPatch,
            TargetDlcName = "bad_mod",
            Operations = new()
            {
                new() { SourcePath = "handling.meta", DestinationPath = @"mods\..\escape.meta", Overwrite = true }
            },
            XmlPatches = new()
        };

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.False(result.Success);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_tempRoot, "escape.meta")));
    }

    // ── Test 7: Overwrite failure preserves the original file ────────────
    // Guards the temp-then-move write discipline: a mid-copy failure while
    // overwriting an existing file must never truncate the original, and must
    // leave no partial .tmp files behind.

    [Fact]
    public async Task Executor_OverwriteFailsMidCopy_OriginalPreservedAndNoTempLeftover()
    {
        var destRel = @"mods\existing.dll";
        var destPath = Path.Combine(_tempRoot, destRel);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var originalContent = new byte[] { 100, 101, 102, 103, 104 };
        File.WriteAllBytes(destPath, originalContent);

        var archive = new FakeArchive(new[]
        {
            new FakeArchiveEntry("existing.dll",
                () => new ThrowingStream(new byte[] { 1, 2, 3, 4, 5, 6 }, failAfter: 2),
                size: 6)
        });
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "overwrite_mod",
            Operations = new()
            {
                new() { SourcePath = "existing.dll", DestinationPath = destRel, Overwrite = true }
            },
            XmlPatches = new()
        };

        var result = await _executor.ExecuteAsync(plan, archive, _tempRoot);

        Assert.False(result.Success);
        Assert.True(File.Exists(destPath), "original file must survive a failed overwrite");
        Assert.Equal(originalContent, File.ReadAllBytes(destPath));

        var leftoverTemps = Directory.GetFiles(Path.GetDirectoryName(destPath)!, "*.tmp");
        Assert.Empty(leftoverTemps);
    }

    private void SetupDlcListXml(string targetRoot)
    {
        var dlclistDir = Path.Combine(targetRoot, @"mods\update\update.rpf\common\data");
        Directory.CreateDirectory(dlclistDir);

        var dlclistPath = Path.Combine(dlclistDir, "dlclist.xml");
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<SMandatoryPacksData>
  <Paths>
  </Paths>
</SMandatoryPacksData>";

        File.WriteAllText(dlclistPath, xmlContent);
    }
}
