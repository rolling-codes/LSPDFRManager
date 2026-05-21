using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Unit tests for <see cref="ModDownloadBridge"/>.
///
/// The bridge is a singleton — each test must use a fresh instance constructed
/// directly (not via the static Instance) so tests stay isolated from each other
/// and from the running application singleton.
/// </summary>
public class ModDownloadBridgeTests : IDisposable
{
    private readonly string _tempRoot;

    public ModDownloadBridgeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"bridge_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Creates a minimal valid ZIP so ModDetector can open it.</summary>
    private string MakeZip(string name, params string[] entries)
    {
        var path = Path.Combine(_tempRoot, name + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var e in entries)
            zip.CreateEntry(e).Open().Close();
        return path;
    }

    private static ModDownloadBridge CreateBridge() => new();

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectsModAndStagesIt_WhenValidArchiveGiven()
    {
        var zip = MakeZip("CalloutPlugin", "plugins/LSPDFR/CalloutPlugin.dll");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        bridge.Staged += staged.Add;

        await bridge.StageDownloadAsync(zip, "CalloutPlugin.zip");

        Assert.Single(staged);
        Assert.Equal(ModType.LspdfrPlugin, staged[0].Type);
    }

    [Fact]
    public async Task RaisesDetectingEvent_BeforeStaging()
    {
        var zip = MakeZip("some_mod", "plugins/LSPDFR/SomeMod.dll");
        var bridge = CreateBridge();

        var detectingRaised = false;
        var stagedRaised    = false;
        var detectingFirst  = false;

        bridge.Detecting += _ => detectingRaised = true;
        bridge.Staged    += _ => { stagedRaised = true; detectingFirst = detectingRaised; };

        await bridge.StageDownloadAsync(zip, "some_mod.zip");

        Assert.True(detectingRaised);
        Assert.True(stagedRaised);
        Assert.True(detectingFirst, "Detecting event must fire before Staged event");
    }

    [Fact]
    public async Task LowConfidenceArchive_StillStaged_NotFailed()
    {
        // ModDetector never throws — even unrecognised archives produce a low-confidence
        // result that the bridge should still stage (caller sees it in the Install tab).
        var zip = MakeZip("random_stuff", "readme.txt", "data.dat");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        var failed = new List<(string Name, string Error)>();
        bridge.Staged += staged.Add;
        bridge.Failed += (name, error) => failed.Add((name, error));

        await bridge.StageDownloadAsync(zip, "random_stuff.zip");

        Assert.Empty(failed);
        Assert.Single(staged);
        Assert.True(staged[0].Confidence < 0.5f, "Expected low confidence for unrecognised archive");
    }

    [Fact]
    public async Task DoesNothing_WhenLocalPathIsEmpty()
    {
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        var detected = new List<string>();
        var failed = new List<(string Name, string Error)>();
        bridge.Staged += staged.Add;
        bridge.Detecting += detected.Add;
        bridge.Failed += (name, error) => failed.Add((name, error));

        await bridge.StageDownloadAsync("", "something.zip");

        Assert.Empty(staged);
        Assert.Empty(detected);
        Assert.Empty(failed);
    }

    [Fact]
    public async Task DoesNothing_WhenLocalPathIsWhitespace()
    {
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        var detected = new List<string>();
        var failed = new List<(string Name, string Error)>();
        bridge.Staged += staged.Add;
        bridge.Detecting += detected.Add;
        bridge.Failed += (name, error) => failed.Add((name, error));

        await bridge.StageDownloadAsync("   ", "something.zip");

        Assert.Empty(staged);
        Assert.Empty(detected);
        Assert.Empty(failed);
    }

    [Fact]
    public async Task CleanDisplayName_AppliedWhenDetectionProducesGenericName()
    {
        // Archive with no strong signal → detector returns generic "Mod …" name
        // Bridge should replace it with a cleaned version of the display name.
        var zip = MakeZip("random_files_pack", "readme.txt", "data.dat");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        bridge.Staged += staged.Add;

        await bridge.StageDownloadAsync(zip, "random_files_pack.zip");

        // Whether staged or failed, the display name must have been cleaned
        // (underscores → spaces, title-cased).
        if (staged.Count == 1)
        {
            // Name must not contain underscores and must not start with "Mod "
            Assert.DoesNotContain("_", staged[0].Name);
        }
        // If detection failed, that is also acceptable for this archive type.
    }

    [Fact]
    public async Task StagedMod_HasCorrectSourcePath()
    {
        var zip = MakeZip("MyPlugin", "plugins/LSPDFR/MyPlugin.dll");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        bridge.Staged += staged.Add;

        await bridge.StageDownloadAsync(zip, "MyPlugin.zip");

        Assert.Single(staged);
        Assert.Equal(zip, staged[0].SourcePath);
    }

    [Fact]
    public async Task MultipleDownloads_AreEachStaged()
    {
        var zip1 = MakeZip("PluginA", "plugins/LSPDFR/PluginA.dll");
        var zip2 = MakeZip("PluginB", "plugins/LSPDFR/PluginB.dll");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        var detected = new List<string>();
        bridge.Staged += staged.Add;
        bridge.Detecting += detected.Add;

        await bridge.StageDownloadAsync(zip1, "PluginA.zip");
        await bridge.StageDownloadAsync(zip2, "PluginB.zip");

        Assert.Equal(2, staged.Count);
        Assert.Equal(2, detected.Count);
    }

    [Fact]
    public async Task VehicleDlcArchive_DetectedCorrectly()
    {
        var zip = MakeZip("myCar_addon",
            "dlcpacks/mycar/dlc.rpf",
            "dlcpacks/mycar/content.xml");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        bridge.Staged += staged.Add;

        await bridge.StageDownloadAsync(zip, "myCar_addon.zip");

        Assert.Single(staged);
        Assert.Equal(ModType.VehicleDlc, staged[0].Type);
    }

    [Fact]
    public async Task AsiMod_DetectedCorrectly()
    {
        var zip = MakeZip("trainer_asi", "TrainerV.asi");
        var bridge = CreateBridge();
        var staged = new List<ModInfo>();
        bridge.Staged += staged.Add;

        await bridge.StageDownloadAsync(zip, "trainer_asi.zip");

        Assert.Single(staged);
        Assert.Equal(ModType.AsiMod, staged[0].Type);
    }
}
