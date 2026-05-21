namespace LSPDFRManager.Tests;

using System.Text.Json;
using LSPDFRManager.OivPipeline;
using LSPDFRManager.OivPipeline.Models;
using Xunit;

public class OivPipelineTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"oivpipe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static void CreateBundle(string dir, IEnumerable<(string relativePath, string content)> files)
    {
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(dir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
    }

    private static void WriteManifest(string dir, object manifestData)
    {
        var json = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json);
    }

    [Fact]
    public async Task VehicleAddon_ValidBundle_ProducesInstallPlan()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("myaddon/vehicles.meta", "meta")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon", packageName = "My Addon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success);
        Assert.Equal("VehicleAddon", result.Report.DetectedType);
        Assert.Contains(result.Report.InstallOperations,
            op => op is PatchXmlOperation p && p.IdempotencyKey.Contains("myaddon"));
    }

    [Fact]
    public async Task VehicleAddon_MissingDlcPackName_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("myaddon/dlc.rpf", "rpf")]);
        WriteManifest(bundleDir, new { type = "vehicle_addon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.True(result.RefusalReasons.Count > 0);
    }

    [Fact]
    public async Task VehicleReplace_MissingReplaceSlot_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("car.yft", "yft"),
            ("car.ytd", "ytd")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_replace" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.Contains(result.Report.RefusalReasons,
            r => r.Contains("replaceSlot") || r.Contains("replace_slot"));
    }

    [Fact]
    public async Task AmbiguousBundle_MixedAddonReplace_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("car.yft", "yft"),
            ("car.ytd", "ytd")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.Contains(result.Report.ValidationResults,
            g => g.Name == "no_mixed_addon_replace" && !g.Passed);
    }

    [Fact]
    public async Task Els_ConfigOnly_InstallsToPackDefault()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("ELS/pack_default/mycar.xml", "<els/>")]);
        WriteManifest(bundleDir, new { type = "els", configOnly = true });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success);
        Assert.All(
            result.Report.InstallOperations.OfType<CopyOperation>(),
            c => Assert.StartsWith("ELS/pack_default/", c.TargetGamePath));
    }

    [Fact]
    public async Task RphPlugin_WithoutManifest_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("MyPlugin.dll", "dll")]);

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.True(result.RefusalReasons.Count > 0);
    }

    [Fact]
    public async Task ShvdnScript_WithoutManifest_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("MyScript.dll", "dll")]);

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task VehicleAddon_NoDuplicateDlclistPatch()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("myaddon/some.yft", "yft")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success);
        var patches = result.Report.InstallOperations
            .OfType<PatchXmlOperation>()
            .Where(p => p.IdempotencyKey.Contains("myaddon"))
            .ToList();
        Assert.Equal(1, patches.Count);
    }

    [Fact]
    public async Task DryRun_DoesNotWriteBuildReport()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("myaddon/dlc.rpf", "rpf")]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(File.Exists(Path.Combine(outputDir, "build_report.json")));
        Assert.True(result.Report.DryRun);
        Assert.Null(result.ReportPath);
    }

    [Fact]
    public async Task FailedBuild_ReportContainsRefusalReasons()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("car.yft", "yft"),
            ("car.ytd", "ytd")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_replace" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: false);

        Assert.False(result.Success);
        Assert.True(result.Report.RefusalReasons.Count > 0);
        Assert.True(File.Exists(Path.Combine(outputDir, "build_report.json")));

        var json = File.ReadAllText(Path.Combine(outputDir, "build_report.json"));
        using var doc = JsonDocument.Parse(json);
        var refusalReasons = doc.RootElement.GetProperty("refusalReasons");
        Assert.Equal(JsonValueKind.Array, refusalReasons.ValueKind);
        Assert.True(refusalReasons.GetArrayLength() > 0);
    }
}
