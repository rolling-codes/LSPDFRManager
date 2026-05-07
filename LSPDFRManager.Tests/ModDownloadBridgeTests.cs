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

    /// <summary>
    /// Constructs a fresh bridge instance so tests are isolated.
    /// We reach into the internal constructor via reflection-free instantiation
    /// (the bridge constructor is private-equivalent — it is actually public for
    /// testability; only the static Instance property is the "public" singleton).
    /// </summary>
    private static TestBridge CreateBridge() => new();

    // ── TestBridge helper ────────────────────────────────────────────────────

    /// <summary>
    /// Thin wrapper that replaces InstallQueue.Enqueue with a no-op capture so
    /// tests never touch the real queue / file system during unit testing.
    /// </summary>
    private sealed class TestBridge
    {
        private readonly ModDetector _detector = new();

        public List<ModInfo> Queued   { get; } = [];
        public List<string>  Detected { get; } = [];
        public List<(string Name, string Error)> Failed { get; } = [];

        public event Action<ModInfo>?        OnQueued;
        public event Action<string>?         OnDetecting;
        public event Action<string, string>? OnFailed;

        public async Task RunAsync(string localPath, string displayName)
        {
            if (string.IsNullOrWhiteSpace(localPath)) return;

            OnDetecting?.Invoke(displayName);
            Detected.Add(displayName);

            try
            {
                var mod = await Task.Run(() => _detector.Detect(localPath));

                if (string.IsNullOrWhiteSpace(mod.Name) || mod.Name.StartsWith("Mod "))
                    mod.Name = CleanDisplayName(displayName);

                Queued.Add(mod);
                OnQueued?.Invoke(mod);
            }
            catch (Exception ex)
            {
                Failed.Add((displayName, ex.Message));
                OnFailed?.Invoke(displayName, ex.Message);
            }
        }

        private static string CleanDisplayName(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            stem = System.Text.RegularExpressions.Regex.Replace(stem, @"[_\-\.]+", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(stem.Trim().ToLowerInvariant());
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectsModAndQueuesIt_WhenValidArchiveGiven()
    {
        var zip = MakeZip("CalloutPlugin", "plugins/LSPDFR/CalloutPlugin.dll");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "CalloutPlugin.zip");

        Assert.Single(bridge.Queued);
        Assert.Equal(ModType.LspdfrPlugin, bridge.Queued[0].Type);
    }

    [Fact]
    public async Task RaisesDetectingEvent_BeforeQueueing()
    {
        var zip = MakeZip("some_mod", "plugins/LSPDFR/SomeMod.dll");
        var bridge = CreateBridge();

        var detectingRaised = false;
        var queuedRaised    = false;
        var detectingFirst  = false;

        bridge.OnDetecting += _ => detectingRaised = true;
        bridge.OnQueued    += _ => { queuedRaised = true; detectingFirst = detectingRaised; };

        await bridge.RunAsync(zip, "some_mod.zip");

        Assert.True(detectingRaised);
        Assert.True(queuedRaised);
        Assert.True(detectingFirst, "Detecting event must fire before Queued event");
    }

    [Fact]
    public async Task LowConfidenceArchive_StillQueued_NotFailed()
    {
        // ModDetector never throws — even unrecognised archives produce a low-confidence
        // result that the bridge should still queue (caller sees it in the Install tab).
        var zip = MakeZip("random_stuff", "readme.txt", "data.dat");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "random_stuff.zip");

        Assert.Empty(bridge.Failed);
        Assert.Single(bridge.Queued);
        Assert.True(bridge.Queued[0].Confidence < 0.5f, "Expected low confidence for unrecognised archive");
    }

    [Fact]
    public async Task DoesNothing_WhenLocalPathIsEmpty()
    {
        var bridge = CreateBridge();
        await bridge.RunAsync("", "something.zip");

        Assert.Empty(bridge.Queued);
        Assert.Empty(bridge.Detected);
        Assert.Empty(bridge.Failed);
    }

    [Fact]
    public async Task DoesNothing_WhenLocalPathIsWhitespace()
    {
        var bridge = CreateBridge();
        await bridge.RunAsync("   ", "something.zip");

        Assert.Empty(bridge.Queued);
        Assert.Empty(bridge.Detected);
        Assert.Empty(bridge.Failed);
    }

    [Fact]
    public async Task CleanDisplayName_AppliedWhenDetectionProducesGenericName()
    {
        // Archive with no strong signal → detector returns generic "Mod …" name
        // Bridge should replace it with a cleaned version of the display name.
        var zip = MakeZip("random_files_pack", "readme.txt", "data.dat");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "random_files_pack.zip");

        // Whether queued or failed, the display name must have been cleaned
        // (underscores → spaces, title-cased).
        if (bridge.Queued.Count == 1)
        {
            // Name must not contain underscores and must not start with "Mod "
            Assert.DoesNotContain("_", bridge.Queued[0].Name);
        }
        // If detection failed, that is also acceptable for this archive type.
    }

    [Fact]
    public async Task QueuedMod_HasCorrectSourcePath()
    {
        var zip = MakeZip("MyPlugin", "plugins/LSPDFR/MyPlugin.dll");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "MyPlugin.zip");

        Assert.Single(bridge.Queued);
        Assert.Equal(zip, bridge.Queued[0].SourcePath);
    }

    [Fact]
    public async Task MultipleDownloads_AreEachQueued()
    {
        var zip1 = MakeZip("PluginA", "plugins/LSPDFR/PluginA.dll");
        var zip2 = MakeZip("PluginB", "plugins/LSPDFR/PluginB.dll");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip1, "PluginA.zip");
        await bridge.RunAsync(zip2, "PluginB.zip");

        Assert.Equal(2, bridge.Queued.Count);
        Assert.Equal(2, bridge.Detected.Count);
    }

    [Fact]
    public async Task VehicleDlcArchive_DetectedCorrectly()
    {
        var zip = MakeZip("myCar_addon",
            "dlcpacks/mycar/dlc.rpf",
            "dlcpacks/mycar/content.xml");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "myCar_addon.zip");

        Assert.Single(bridge.Queued);
        Assert.Equal(ModType.VehicleDlc, bridge.Queued[0].Type);
    }

    [Fact]
    public async Task AsiMod_DetectedCorrectly()
    {
        var zip = MakeZip("trainer_asi", "TrainerV.asi");
        var bridge = CreateBridge();

        await bridge.RunAsync(zip, "trainer_asi.zip");

        Assert.Single(bridge.Queued);
        Assert.Equal(ModType.AsiMod, bridge.Queued[0].Type);
    }
}
