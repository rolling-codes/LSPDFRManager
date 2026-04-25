using LSPDFRManager.OpenIv.CarInstall;
using LSPDFRManager.OpenIv.CarInstall.Models;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Integration tests for OpenIV car install pipeline: Analyzer → Planner → Validator
/// Validates deterministic contract boundary before Executor side effects.
/// </summary>
public class OpenIvIntegrationTests
{
    private readonly OpenIvInstallPlanner _planner = new();

    // ── Test 1: ReplaceVehicle Flow ──────────────────────────────────────

    [Fact]
    public void ReplaceVehicle_ShouldGenerateValidPlan()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = _planner.BuildPlan(CarInstallType.ReplaceVehicle, archive, "test_car");

        OpenIvInstallPlanValidator.Validate(plan);

        Assert.Equal(CarInstallType.ReplaceVehicle, plan.Type);
        Assert.Equal("test_car", plan.TargetDlcName);
        Assert.NotEmpty(plan.Operations);
        Assert.All(plan.Operations, op =>
            Assert.StartsWith(@"mods\", op.DestinationPath, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(plan.XmlPatches);
        Assert.False(plan.RequiresDlcListPatch);
    }

    // ── Test 2: AddonDLC with dlclist Patch ──────────────────────────────

    [Fact]
    public void AddonDLC_ShouldGenerateDlclistPatch()
    {
        var archive = FakeArchiveFactory.CreateAddonDLC();
        var plan = _planner.BuildPlan(CarInstallType.AddonDLC, archive, "test_dlc");

        OpenIvInstallPlanValidator.Validate(plan);

        Assert.Equal(CarInstallType.AddonDLC, plan.Type);
        Assert.Equal("test_dlc", plan.TargetDlcName);
        Assert.NotEmpty(plan.Operations);
        Assert.Single(plan.XmlPatches);
        Assert.Contains("dlclist.xml", plan.XmlPatches[0].FilePath);
        Assert.Contains("test_dlc", plan.XmlPatches[0].Value);
        Assert.True(plan.RequiresDlcListPatch);
    }

    // ── Test 3: ConfigPatch Meta-Only ────────────────────────────────────

    [Fact]
    public void ConfigPatch_ShouldOnlyContainMetaFiles()
    {
        var archive = FakeArchiveFactory.CreateConfigPatch();
        var plan = _planner.BuildPlan(CarInstallType.ConfigPatch, archive, "meta_mod");

        OpenIvInstallPlanValidator.Validate(plan);

        Assert.Equal(CarInstallType.ConfigPatch, plan.Type);
        Assert.Equal("meta_mod", plan.TargetDlcName);
        Assert.NotEmpty(plan.Operations);
        Assert.All(plan.Operations, op =>
            Assert.EndsWith(".meta", op.SourcePath, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(plan.XmlPatches);
        Assert.False(plan.RequiresDlcListPatch);
    }

    // ── Test 4: Invalid Path (Traversal Safety) ──────────────────────────

    [Fact]
    public void ConfigPatch_ShouldRejectPathTraversal()
    {
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ConfigPatch,
            TargetDlcName = "bad_mod",
            Operations = new()
            {
                new() { SourcePath = "handling.meta", DestinationPath = @"..\..\escape.meta", Overwrite = true }
            },
            XmlPatches = new()
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenIvInstallPlanValidator.Validate(plan));

        Assert.Contains("mods", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 5: Invalid DLC Name (Reserved Name) ─────────────────────────

    [Fact]
    public void AddonDLC_ShouldRejectReservedDlcName()
    {
        var archive = FakeArchiveFactory.CreateAddonDLC();
        var plan = _planner.BuildPlan(CarInstallType.AddonDLC, archive, "update");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenIvInstallPlanValidator.Validate(plan));

        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 6: ConfigPatch Cannot Have XML Patches ──────────────────────

    [Fact]
    public void ConfigPatch_ShouldRejectXmlPatches()
    {
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ConfigPatch,
            TargetDlcName = "test",
            Operations = new()
            {
                new() { SourcePath = "test.meta", DestinationPath = @"mods\test.meta", Overwrite = true }
            },
            XmlPatches = new()
            {
                new() { FilePath = @"mods\update\update.rpf\common\data\dlclist.xml", XPath = "/test", Value = "test" }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenIvInstallPlanValidator.Validate(plan));

        Assert.Contains("ConfigPatch", ex.Message);
    }

    // ── Test 7: AddonDLC Must Have XML Patches ───────────────────────────

    [Fact]
    public void AddonDLC_ShouldRequireXmlPatches()
    {
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.AddonDLC,
            TargetDlcName = "test_dlc",
            Operations = new()
            {
                new() { SourcePath = "car.yft", DestinationPath = @"mods\update\x64\dlcpacks\test_dlc\car.yft", Overwrite = true }
            },
            XmlPatches = new()
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenIvInstallPlanValidator.Validate(plan));

        Assert.Contains("AddonDLC", ex.Message);
        Assert.Contains("XML patch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cross-Layer Consistency Tests ────────────────────────────────────

    [Fact]
    public void MixedAssets_ShouldDefaultToReplaceVehicle()
    {
        var archive = FakeArchiveFactory.CreateMixedYftYtdMeta();
        var plan = _planner.BuildPlan(CarInstallType.ReplaceVehicle, archive, "mixed_car");

        OpenIvInstallPlanValidator.Validate(plan);

        Assert.Equal(CarInstallType.ReplaceVehicle, plan.Type);
        Assert.Empty(plan.XmlPatches);
    }

    [Fact]
    public void AllOperationsPreserveSourceFilenames()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = _planner.BuildPlan(CarInstallType.ReplaceVehicle, archive, "test_car");

        OpenIvInstallPlanValidator.Validate(plan);

        foreach (var op in plan.Operations)
        {
            var sourceFilename = Path.GetFileName(op.SourcePath);
            var destFilename = Path.GetFileName(op.DestinationPath);
            Assert.Equal(sourceFilename, destFilename);
        }
    }

    [Fact]
    public void AllPathsNormalizedWithBackslashes()
    {
        var archive = FakeArchiveFactory.CreateAddonDLC();
        var plan = _planner.BuildPlan(CarInstallType.AddonDLC, archive, "test_dlc");

        OpenIvInstallPlanValidator.Validate(plan);

        foreach (var op in plan.Operations)
            Assert.DoesNotContain('/', op.DestinationPath);
        foreach (var patch in plan.XmlPatches)
            Assert.DoesNotContain('/', patch.FilePath);
    }
}
