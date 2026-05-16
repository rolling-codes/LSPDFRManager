using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for ModTypeDetectionService — the pure, evidence-based classifier.
/// All inputs are normalized (lowercase, forward-slash) string lists.
/// No file I/O.
/// </summary>
public class ModTypeDetectionServiceTests
{
    private static ModTypeDetectionResult Detect(IReadOnlyList<string> entries, string? archiveName = null)
        => new ModTypeDetectionService().Detect(entries, archiveName);

    // ── OIV Package ──────────────────────────────────────────────────────────

    [Fact]
    public void OivPackage_AssemblyXmlAtRoot_IsOiv()
    {
        var result = Detect(["assembly.xml", "content/update.rpf"]);

        Assert.Equal(ModType.OivPackage, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
        Assert.Contains(result.Evidence, e => e.Contains("assembly.xml"));
    }

    [Fact]
    public void OivPackage_AssemblyXmlOneFolder_IsOiv()
    {
        // assembly.xml nested exactly one level deep (common OIV layout).
        var result = Detect(["mymod/assembly.xml", "mymod/content/update.rpf"]);

        Assert.Equal(ModType.OivPackage, result.PrimaryType);
    }

    [Fact]
    public void OivPackage_AssemblyXmlDeep_NotHighConfidence()
    {
        // assembly.xml buried two+ levels deep is NOT an OIV marker.
        var result = Detect(["a/b/assembly.xml", "a/b/readme.txt"]);

        // May be Unknown or ConfigPreset, but NOT OIV at high confidence.
        if (result.PrimaryType == ModType.OivPackage)
            Assert.True(result.Confidence < 0.75f);
    }

    // ── ASI Mod ──────────────────────────────────────────────────────────────

    [Fact]
    public void AsiMod_SingleAsiFile_IsAsi()
    {
        var result = Detect(["scripthookv.asi", "readme.txt"]);

        Assert.Equal(ModType.AsiMod, result.PrimaryType);
        Assert.True(result.Confidence >= 0.45f);
        Assert.Contains(result.Evidence, e => e.Contains(".asi"));
    }

    [Fact]
    public void AsiMod_MultipleAsiFiles_AllListedInEvidence()
    {
        var result = Detect(["trainer.asi", "helper.asi", "readme.txt"]);

        Assert.Equal(ModType.AsiMod, result.PrimaryType);
        Assert.Equal(2, result.Evidence.Count(e => e.Contains(".asi file:")));
    }

    [Fact]
    public void AsiMod_NameKeywordBoostsConfidence()
    {
        var withKeyword    = Detect(["tool.asi"], archiveName: "scripthook_v2.zip");
        var withoutKeyword = Detect(["tool.asi"], archiveName: "randommod.zip");

        Assert.True(withKeyword.Confidence >= withoutKeyword.Confidence);
    }

    // ── ScriptHookVDotNet Script ─────────────────────────────────────────────

    [Fact]
    public void Script_CsInScriptsFolder_IsScript()
    {
        var result = Detect(["scripts/myscript.cs", "scripts/myscript.ini"]);

        Assert.Equal(ModType.Script, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
        Assert.Contains(result.Evidence, e => e.Contains("scripts/"));
    }

    [Fact]
    public void Script_LuaAnywhere_IsScript()
    {
        var result = Detect(["client.lua", "server.lua"]);

        Assert.Equal(ModType.Script, result.PrimaryType);
    }

    [Fact]
    public void Script_CsOutsideScriptsFolder_LowerConfidence()
    {
        // Loose .cs files are weaker evidence — could be source code.
        var inFolder  = Detect(["scripts/mod.cs"]);
        var loose     = Detect(["src/mod.cs"]);

        Assert.True(inFolder.Confidence > loose.Confidence);
    }

    // ── DLC Pack ─────────────────────────────────────────────────────────────

    [Fact]
    public void DlcPack_DlcPacksPath_IsDlc()
    {
        var result = Detect(["mods/update/x64/dlcpacks/myaddon/dlc.rpf"]);

        Assert.Equal(ModType.VehicleDlc, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
        Assert.Contains(result.Evidence, e => e.Contains("dlcpacks/"));
    }

    [Fact]
    public void DlcPack_DlcRpfAlone_IsDlcMedium()
    {
        var result = Detect(["dlc.rpf"]);

        Assert.Equal(ModType.VehicleDlc, result.PrimaryType);
        Assert.True(result.Confidence >= 0.45f);
    }

    // ── LSPDFR Plugin ────────────────────────────────────────────────────────

    [Fact]
    public void LspdfrPlugin_DllInPluginsLspdfr_IsPlugin()
    {
        var result = Detect(["plugins/lspdfr/myplugin.dll", "plugins/lspdfr/myplugin.ini"]);

        Assert.Equal(ModType.LspdfrPlugin, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
        Assert.Contains(result.Evidence, e => e.Contains("plugins/lspdfr/"));
    }

    [Fact]
    public void LspdfrPlugin_DllInPluginsOnly_MediumConfidence()
    {
        var result = Detect(["plugins/someplugin.dll"]);

        Assert.Equal(ModType.LspdfrPlugin, result.PrimaryType);
        Assert.True(result.Confidence >= 0.45f);
    }

    // ── EUP Clothing ─────────────────────────────────────────────────────────

    [Fact]
    public void Eup_YddInComponentPeds_IsEup()
    {
        var result = Detect([
            "componentpeds/mp_f_freemode_01/jbib_000_u.ydd",
            "componentpeds/mp_f_freemode_01/jbib_000_u.ytd",
        ]);

        Assert.Equal(ModType.Eup, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
    }

    // ── Map / MLO ────────────────────────────────────────────────────────────

    [Fact]
    public void Map_YmapFiles_IsMap()
    {
        var result = Detect(["myinterior_mlo.ymap", "myinterior_mlo.ytyp"]);

        Assert.Equal(ModType.Map, result.PrimaryType);
        Assert.True(result.Confidence >= 0.45f);
    }

    // ── Sound Pack ───────────────────────────────────────────────────────────

    [Fact]
    public void Sound_AwcFile_IsSound()
    {
        var result = Detect(["x64/audio/sfx/sirens_new.awc"]);

        Assert.Equal(ModType.Sound, result.PrimaryType);
    }

    // ── Config-only ──────────────────────────────────────────────────────────

    [Fact]
    public void ConfigOnly_AllIniAndXml_IsConfigPreset()
    {
        var result = Detect(["config.ini", "settings.xml", "readme.txt"]);

        Assert.Equal(ModType.ConfigPreset, result.PrimaryType);
        Assert.True(result.Confidence >= 0.75f);
        Assert.Contains(result.Evidence, e => e.Contains("configuration"));
    }

    [Fact]
    public void ConfigOnly_WithBinary_IsNotConfigPreset()
    {
        // A single .dll breaks the "config-only" classification.
        var result = Detect(["config.ini", "plugin.dll"]);

        Assert.NotEqual(ModType.ConfigPreset, result.PrimaryType);
    }

    // ── Unknown ──────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_RandomExtensions_IsUnknown()
    {
        var result = Detect(["something.xyz", "data.bin"]);

        Assert.Equal(ModType.Unknown, result.PrimaryType);
        Assert.True(result.Warnings.Count > 0);
        Assert.Contains(result.Warnings, w => w.Contains("unclassified"));
    }

    [Fact]
    public void Unknown_EmptyEntries_IsUnknown()
    {
        var result = Detect([]);

        Assert.Equal(ModType.Unknown, result.PrimaryType);
    }

    // ── Nested root (single top-level folder wrapper) ─────────────────────────

    [Fact]
    public void NestedRoot_PluginUnderWrapperFolder_StillDetected()
    {
        // Archive layout: MyMod_v1.2/plugins/lspdfr/plugin.dll
        var result = Detect(["mymoad_v1.2/plugins/lspdfr/plugin.dll"]);

        Assert.Equal(ModType.LspdfrPlugin, result.PrimaryType);
    }

    [Fact]
    public void NestedRoot_DlcUnderWrapperFolder_StillDetected()
    {
        var result = Detect(["mymod_v1.0/mods/update/x64/dlcpacks/myaddon/dlc.rpf"]);

        Assert.Equal(ModType.VehicleDlc, result.PrimaryType);
    }

    [Fact]
    public void NestedRoot_OivUnderWrapperFolder_StillDetected()
    {
        var result = Detect(["mymod/assembly.xml", "mymod/content/update.rpf"]);

        Assert.Equal(ModType.OivPackage, result.PrimaryType);
    }

    // ── Mixed archives ────────────────────────────────────────────────────────

    [Fact]
    public void Mixed_AsiAndScript_IsMixedFlagged()
    {
        // Bundle containing both an ASI and a SHVDN script in scripts/ folder.
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        Assert.True(result.IsMixed);
        Assert.True(result.SecondaryTypes.Count > 0);
        Assert.Contains(result.Warnings, w => w.Contains("multiple types"));
    }

    [Fact]
    public void Mixed_SecondaryTypePresentInResult()
    {
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        var allTypes = new[] { result.PrimaryType }
            .Concat(result.SecondaryTypes.Select(s => s.Type))
            .ToHashSet();

        Assert.Contains(ModType.AsiMod,  allTypes);
        Assert.Contains(ModType.Script,  allTypes);
    }

    [Fact]
    public void Mixed_EachSecondaryHasEvidence()
    {
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        foreach (var secondary in result.SecondaryTypes)
            Assert.True(secondary.Evidence.Count > 0, $"No evidence for secondary type {secondary.Type}");
    }

    // ── Ambiguous archives ────────────────────────────────────────────────────

    [Fact]
    public void Ambiguous_WeakSignalsOnly_LowConfidence()
    {
        // Archive name has a keyword ("trainer") but the single .bin file matches no
        // rule above the primary threshold — expect Unknown or very low confidence.
        var result = Detect(["data.bin"], archiveName: "trainer_something.zip");

        Assert.True(result.Confidence < 0.45f || result.PrimaryType == ModType.Unknown);
    }

    [Fact]
    public void Ambiguous_ConfigWithPluginName_StillConfigIfOnlyConfigs()
    {
        // Config files named after a plugin — should still classify as ConfigPreset.
        var result = Detect(["lspdfr_settings.ini", "callout_config.xml"]);

        Assert.Equal(ModType.ConfigPreset, result.PrimaryType);
    }

    // ── Evidence quality ─────────────────────────────────────────────────────

    [Fact]
    public void Evidence_AlwaysNonEmptyForKnownType()
    {
        var cases = new[]
        {
            Detect(["scripthookv.asi"]),
            Detect(["scripts/mod.cs"]),
            Detect(["assembly.xml"]),
            Detect(["dlcpacks/myaddon/dlc.rpf"]),
            Detect(["plugins/lspdfr/plugin.dll"]),
        };

        foreach (var result in cases)
            Assert.True(result.Evidence.Count > 0, $"No evidence for {result.PrimaryType}");
    }

    [Fact]
    public void Evidence_UnknownHasNoEvidence()
    {
        var result = Detect(["something.xyz"]);

        Assert.Equal(ModType.Unknown, result.PrimaryType);
        Assert.Empty(result.Evidence);
    }

    // ── Confidence ordering invariant ─────────────────────────────────────────

    [Fact]
    public void SecondaryTypes_AlwaysOrderedByDescendingConfidence()
    {
        // Use a rich mixed archive to ensure multiple secondaries.
        var result = Detect([
            "trainer.asi",
            "scripts/mod.cs",
            "config.ini",
        ]);

        for (int i = 1; i < result.SecondaryTypes.Count; i++)
            Assert.True(
                result.SecondaryTypes[i - 1].Confidence >= result.SecondaryTypes[i].Confidence,
                "Secondary types must be ordered descending by confidence");
    }
}
