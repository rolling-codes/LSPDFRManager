namespace LSPDFRManager.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using LSPDFRManager.OivPipeline;
using LSPDFRManager.OivPipeline.Models;
using Xunit;

/// <summary>
/// Hardening tests: serialization, determinism, mixed-bundle regressions, StubOivBuilder, SirenPack.
/// Complements the baseline OivPipelineTests.cs.
/// </summary>
public class OivPipelineHardeningTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private static readonly JsonSerializerOptions SerializeOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"oivhard_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
    }

    private static void CreateBundle(string dir, IEnumerable<(string relativePath, string content)> files)
    {
        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    private static void WriteManifest(string dir, object data) =>
        File.WriteAllText(
            Path.Combine(dir, "manifest.json"),
            JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

    // ── Serialization ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildReport_SerializesToCamelCaseJson()
    {
        var report = new BuildReport
        {
            PackageName = "TestPack",
            DetectedType = "VehicleAddon",
            ConfidenceSource = "Manifest",
            RefusalReasons = ["missing dlc.rpf"],
            Timestamp = DateTimeOffset.UtcNow,
            DryRun = true
        };

        var json = JsonSerializer.Serialize(report, SerializeOpts);

        // Top-level property names must be camelCase
        Assert.Contains("\"packageName\":", json);
        Assert.Contains("\"detectedType\":", json);
        Assert.Contains("\"refusalReasons\":", json);
        Assert.Contains("\"dryRun\":", json);
        // Must NOT contain PascalCase keys
        Assert.DoesNotContain("\"PackageName\":", json);
        Assert.DoesNotContain("\"DetectedType\":", json);
    }

    [Fact]
    public void InstallOperations_SerializeWithPolymorphicDiscriminator()
    {
        InstallOperation copy = new CopyOperation("src/file.yft", "mods/update/x64/file.yft");
        InstallOperation patch = new PatchXmlOperation(
            "mods/update/update.rpf/common/data/dlclist.xml",
            "<Item>dlcpacks:/myaddon/</Item>",
            "dlcpacks:/myaddon/");
        InstallOperation ensure = new EnsureModsCopyOperation("x64e.rpf", "mods/x64e.rpf");

        var copyJson  = JsonSerializer.Serialize(copy, SerializeOpts);
        var patchJson = JsonSerializer.Serialize(patch, SerializeOpts);
        var ensureJson = JsonSerializer.Serialize(ensure, SerializeOpts);

        Assert.Contains("\"kind\":\"copy\"", copyJson);
        Assert.Contains("\"kind\":\"patchXml\"", patchJson);
        Assert.Contains("\"kind\":\"ensureModsCopy\"", ensureJson);
    }

    [Fact]
    public void InstallOperation_RoundTrips_ThroughJson()
    {
        InstallOperation original = new PatchXmlOperation(
            "mods/update/update.rpf/common/data/dlclist.xml",
            "<Item>dlcpacks:/myaddon/</Item>",
            "dlcpacks:/myaddon/",
            "append-unique");

        var json = JsonSerializer.Serialize(original, SerializeOpts);
        var deserialized = JsonSerializer.Deserialize<InstallOperation>(json, SerializeOpts);

        Assert.IsType<PatchXmlOperation>(deserialized);
        var roundTripped = (PatchXmlOperation)deserialized!;
        Assert.Equal("dlcpacks:/myaddon/", roundTripped.IdempotencyKey);
    }

    [Fact]
    public async Task InstallOperations_OrderingIsDeterministic()
    {
        // Two files whose names would sort differently depending on insertion order
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/zzz_last.yft", "y"),
            ("myaddon/aaa_first.yft", "y"),
            ("myaddon/dlc.rpf", "r"),
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success);

        var paths = result.Report.InstallOperations
            .OfType<CopyOperation>()
            .Select(c => c.TargetGamePath)
            .ToList();

        var sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, paths);
    }

    // ── Determinism / fixture ────────────────────────────────────────────────

    [Fact]
    public void BundleScanner_OutputIsOrdinalSorted()
    {
        var dir = MakeTempDir();

        // Create files intentionally out of ordinal order
        File.WriteAllText(Path.Combine(dir, "zzz.txt"), "z");
        File.WriteAllText(Path.Combine(dir, "aaa.txt"), "a");
        File.WriteAllText(Path.Combine(dir, "mmm.txt"), "m");

        var files = BundleScanner.Scan(dir);

        var paths = files.Select(f => f.RelativePath).ToList();
        var sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, paths);
    }

    [Fact]
    public void BundleScanner_HashesAreStable()
    {
        var dir1 = MakeTempDir();
        var dir2 = MakeTempDir();

        const string content = "deterministic content";
        File.WriteAllText(Path.Combine(dir1, "file.txt"), content);
        File.WriteAllText(Path.Combine(dir2, "file.txt"), content);

        var scan1 = BundleScanner.Scan(dir1);
        var scan2 = BundleScanner.Scan(dir2);

        Assert.Equal(scan1[0].ContentHash, scan2[0].ContentHash);
        Assert.NotEmpty(scan1[0].ContentHash);
    }

    [Fact]
    public async Task DryRun_DoesNotMutateInputDirectory()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("myaddon/vehicles.meta", "meta")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon", packageName = "My Addon" });

        var inputFilesBefore = Directory.GetFiles(bundleDir, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

        await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        var inputFilesAfter = Directory.GetFiles(bundleDir, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.Equal(inputFilesBefore, inputFilesAfter);
    }

    [Fact]
    public async Task SameBundleRunTwice_ProducesEquivalentPlans()
    {
        var bundleDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("myaddon/vehicles.meta", "meta"),
            ("myaddon/handling.meta", "h")
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result1 = await new OivBuildPipeline().RunAsync(bundleDir, MakeTempDir(), dryRun: true);
        var result2 = await new OivBuildPipeline().RunAsync(bundleDir, MakeTempDir(), dryRun: true);

        Assert.True(result1.Success);
        Assert.True(result2.Success);

        var ops1 = result1.Report.InstallOperations.OfType<CopyOperation>()
            .Select(c => c.TargetGamePath).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var ops2 = result2.Report.InstallOperations.OfType<CopyOperation>()
            .Select(c => c.TargetGamePath).OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Equal(ops1, ops2);
    }

    // ── HasMixedAddonReplace regressions ─────────────────────────────────────

    [Fact]
    public async Task HasMixedAddonReplace_YftInsideDlcFolder_IsNotMixed()
    {
        // .yft is INSIDE the DLC pack folder — should not trigger mixed detection
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("myaddon/vehicles/car.yft", "yft"),
            ("myaddon/textures/car.ytd", "ytd"),
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success, $"Expected success but got: {string.Join(", ", result.RefusalReasons)}");
        Assert.DoesNotContain(result.Report.ValidationResults, g => g.Name == "no_mixed_addon_replace" && !g.Passed);
    }

    [Fact]
    public async Task HasMixedAddonReplace_LooseYftAtRoot_IsMixed()
    {
        // .yft is at the root of the bundle, outside all DLC folders — should trigger mixed detection
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("car.yft", "yft"),         // loose — outside any DLC pack folder
            ("car.ytd", "ytd"),
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.Contains(result.Report.ValidationResults, g => g.Name == "no_mixed_addon_replace" && !g.Passed);
    }

    [Fact]
    public async Task HasMixedAddonReplace_DlcAndLooseFiles_RefusesWithReason()
    {
        // Explicit check that the refusal reason message is human-readable
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myaddon/dlc.rpf", "rpf"),
            ("unrelated.yft", "yft"),
        ]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        var mixedGate = result.Report.ValidationResults.FirstOrDefault(g => g.Name == "no_mixed_addon_replace");
        Assert.NotNull(mixedGate);
        Assert.NotNull(mixedGate.Reason);
        Assert.NotEmpty(mixedGate.Reason);
    }

    // ── StubOivBuilder ────────────────────────────────────────────────────────

    [Fact]
    public async Task StubOivBuilder_AlwaysRefusesWithExplanation()
    {
        var input = new OivBuildInput(
            PackageName: "test",
            Version: "1.0",
            Author: "tester",
            Operations: [],
            OutputDirectory: Path.GetTempPath());

        var result = await new StubOivBuilder().BuildAsync(input);

        Assert.False(result.Success);
        Assert.Null(result.OutputPath);
        Assert.NotNull(result.Error);
        Assert.Contains("not implemented", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pipeline_NonDryRun_AddsOivBuilderWarning_NotSuccess()
    {
        // Confirms the stub registers as a warning in the report, not a silent success
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("myaddon/dlc.rpf", "rpf")]);
        WriteManifest(bundleDir, new { type = "vehicle_addon", dlcPackName = "myaddon" });

        // Non-dry-run uses the StubOivBuilder
        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: false);

        // Pipeline itself passes (planning succeeded) but OIV generation warns
        Assert.Contains(result.Report.Warnings, w => w.Contains("OIV builder"));
    }

    // ── SirenPack ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SirenPack_WithManifest_AddsRoutingWarning()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir, [("siren_config/police.xml", "<siren/>")]);
        WriteManifest(bundleDir, new { type = "siren_pack", packageName = "MySirens" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success, $"Expected success but: {string.Join(", ", result.RefusalReasons)}");
        Assert.Contains(result.Report.Warnings, w => w.Contains("siren_pack"));
    }

    // ── VehicleReplace with targetArchivePath ──────────────────────────────

    [Fact]
    public async Task VehicleReplace_WithTargetArchivePath_NoReplaceSlotRequired()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("car.yft", "yft"),
            ("car.ytd", "ytd"),
        ]);
        WriteManifest(bundleDir, new
        {
            type = "vehicle_replace",
            targetArchivePath = "mods/update/x64/dlcpacks/patchday1ng/dlc.rpf/x64/levels/gta5/vehicles.rpf"
        });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.True(result.Success, $"Expected success but: {string.Join(", ", result.RefusalReasons)}");
        Assert.All(result.Report.InstallOperations.OfType<CopyOperation>(),
            c => Assert.StartsWith("mods/update/x64/dlcpacks/patchday1ng/", c.TargetGamePath));
    }

    // ── Refusal message quality ────────────────────────────────────────────────

    [Fact]
    public async Task AllFailedGates_HaveNonEmptyReasonStrings()
    {
        // A bundle that fails multiple gates should produce human-readable reasons for each
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        // vehicle_replace with no slot, no target, no yft, no ytd
        CreateBundle(bundleDir, [("readme.txt", "nothing useful")]);
        WriteManifest(bundleDir, new { type = "vehicle_replace", replaceSlot = "adder" });
        // replaceSlot set but KnownSlotMap is empty → target_path_determinable fails
        // no .yft or .ytd → those gates fail

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        var failedGates = result.Report.ValidationResults.Where(g => !g.Passed).ToList();
        Assert.True(failedGates.Count > 0);
        Assert.All(failedGates, g =>
        {
            Assert.NotNull(g.Reason);
            Assert.NotEmpty(g.Reason!);
        });
    }

    [Fact]
    public async Task WeaponAddon_LooseMetaOutsideDlcRpf_Refuses()
    {
        var bundleDir = MakeTempDir();
        var outputDir = MakeTempDir();

        CreateBundle(bundleDir,
        [
            ("myweapons/dlc.rpf", "rpf"),
            ("weapons.meta", "meta"),   // loose — outside dlc.rpf path
        ]);
        WriteManifest(bundleDir, new { type = "weapon_addon", dlcPackName = "myweapons" });

        var result = await new OivBuildPipeline().RunAsync(bundleDir, outputDir, dryRun: true);

        Assert.False(result.Success);
        Assert.Contains(result.Report.ValidationResults,
            g => g.Name == "no_loose_weapon_meta" && !g.Passed);
    }
}
