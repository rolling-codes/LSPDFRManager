using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="ModDetector"/>.
/// Each test builds a minimal directory or ZIP archive that represents a mod
/// category, then asserts on the detected <see cref="ModType"/> and confidence.
/// </summary>
public class ModDetectorTests : IDisposable
{
    private readonly ModDetector _detector = new();
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_tests_{Guid.NewGuid():N}");

    public ModDetectorTests() => Directory.CreateDirectory(_tempRoot);

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private string MakeDir(string name, params string[] relPaths)
    {
        var dir = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(dir);
        foreach (var rel in relPaths)
        {
            var full = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "placeholder");
        }
        return dir;
    }

    private string MakeZip(string name, params string[] entryPaths)
    {
        var zipPath = Path.Combine(_tempRoot, name + ".zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var entry in entryPaths)
            zip.CreateEntry(entry).Open().Close();
        return zipPath;
    }

    // ── ModType detection ──────────────────────────────────────────────────

    [Fact]
    public void Detect_AsiFile_ReturnsAsiMod()
    {
        var dir = MakeDir("trainer", "OpenIV.asi");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.AsiMod, info.Type);
    }

    [Fact]
    public void Detect_LspdfrPluginPath_ReturnsLspdfrPlugin()
    {
        var dir = MakeDir("callout_plugin", @"plugins\lspdfr\MyCallout.dll");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.LspdfrPlugin, info.Type);
    }

    [Fact]
    public void Detect_DlcPacksStructure_ReturnsVehicleDlc()
    {
        var dir = MakeDir("vehicle_addon", @"dlcpacks\myaddon\dlc.rpf");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.VehicleDlc, info.Type);
    }

    [Fact]
    public void Detect_ScriptsCsFile_ReturnsScript()
    {
        var dir = MakeDir("speed_script", @"scripts\SpeedBoost.cs");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.Script, info.Type);
    }

    [Fact]
    public void Detect_YmapFile_ReturnsMap()
    {
        var dir = MakeDir("map_mod", @"interiors\building.ymap");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.Map, info.Type);
    }

    [Fact]
    public void Detect_AwcFile_ReturnsSound()
    {
        var dir = MakeDir("siren_pack", @"audio\sfx\sirens.awc");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.Sound, info.Type);
    }

    [Fact]
    public void Detect_EupClothing_ReturnsEup()
    {
        var dir = MakeDir("eup_uniform", @"eup\ped_male\cop_outfit.ydd");
        var info = _detector.Detect(dir);
        Assert.Equal(ModType.Eup, info.Type);
    }

    [Fact]
    public void Detect_EmptyDirectory_ReturnsMiscOrUnknown()
    {
        var dir = MakeDir("empty_mod");
        var info = _detector.Detect(dir);
        Assert.True(info.Type is ModType.Misc or ModType.Unknown,
            $"Expected Misc or Unknown, got {info.Type}");
    }

    // ── ZIP archive detection ──────────────────────────────────────────────

    [Fact]
    public void Detect_ZipWithAsi_ReturnsAsiMod()
    {
        var zip = MakeZip("scripthook", "ScriptHookV.asi", "ScriptHookV.dll");
        var info = _detector.Detect(zip);
        Assert.Equal(ModType.AsiMod, info.Type);
    }

    [Fact]
    public void Detect_ZipWithDlcPacks_ReturnsVehicleDlc()
    {
        var zip = MakeZip("car_addon", "dlcpacks/myvehicle/dlc.rpf");
        var info = _detector.Detect(zip);
        Assert.Equal(ModType.VehicleDlc, info.Type);
    }

    // ── Confidence ─────────────────────────────────────────────────────────

    [Fact]
    public void Detect_StrongMatch_ReturnsHighOrMediumConfidence()
    {
        var dir = MakeDir("strong_plugin", @"plugins\lspdfr\Arrest.dll");
        var info = _detector.Detect(dir);
        Assert.True(info.Confidence >= 0.45f,
            $"Expected at least medium confidence, got {info.Confidence:P0}");
    }

    [Fact]
    public void Detect_EmptyDir_ReturnsLowConfidence()
    {
        var dir = MakeDir("unknown_mod");
        var info = _detector.Detect(dir);
        Assert.True(info.Confidence < 0.45f,
            $"Expected low confidence for empty dir, got {info.Confidence:P0}");
    }

    // ── Metadata extraction ────────────────────────────────────────────────

    [Fact]
    public void Detect_VersionInFileName_ExtractsVersion()
    {
        var dir = MakeDir("dummy");
        // Rename the directory (we pass the path as the "source")
        var versioned = Path.Combine(_tempRoot, "ELS_v8.4.5");
        Directory.CreateDirectory(versioned);
        var info = _detector.Detect(versioned);
        Assert.Equal("8.4.5", info.Version);
    }

    [Fact]
    public void Detect_DlcPacksZip_ExtractsDlcPackName()
    {
        var zip = MakeZip("myaddon_vehicle", "dlcpacks/coolcar/dlc.rpf");
        var info = _detector.Detect(zip);
        Assert.Equal("coolcar", info.DlcPackName);
    }

    // ── Name cleaning ──────────────────────────────────────────────────────

    [Fact]
    public void Detect_NameWithUnderscoresAndVersion_ProducesCleanTitle()
    {
        var dir = Path.Combine(_tempRoot, "my_cool_mod_v2.0");
        Directory.CreateDirectory(dir);
        var info = _detector.Detect(dir);
        // Version suffix should be stripped; separators converted to spaces
        Assert.DoesNotContain("_", info.Name);
        Assert.DoesNotContain("v2.0", info.Name, StringComparison.OrdinalIgnoreCase);
    }

    // ── Warnings ──────────────────────────────────────────────────────────

    [Fact]
    public void Detect_LowConfidence_AddsWarning()
    {
        var dir = MakeDir("mystery_files", "readme.txt");
        var info = _detector.Detect(dir);
        if (info.Confidence < 0.30f)
            Assert.NotEmpty(info.Warnings);
    }
}
