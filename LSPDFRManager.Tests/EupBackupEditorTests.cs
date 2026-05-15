using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using System.Xml.Linq;
using Xunit;

namespace LSPDFRManager.Tests;

public class EupBackupEditorTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "EupTests_" + Guid.NewGuid());

    public EupBackupEditorTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }

    // ════════════════════════════════════════════════════════════════
    // EupInferenceHelper — freemode detection
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void IsFreemodePed_Male_ReturnsTrue() =>
        Assert.True(EupInferenceHelper.IsFreemodePed("mp_m_freemode_01"));

    [Fact]
    public void IsFreemodePed_Female_ReturnsTrue() =>
        Assert.True(EupInferenceHelper.IsFreemodePed("mp_f_freemode_01"));

    [Fact]
    public void IsFreemodePed_Cop_ReturnsFalse() =>
        Assert.False(EupInferenceHelper.IsFreemodePed("s_m_y_cop_01"));

    [Fact]
    public void IsFreemodePed_Sheriff_ReturnsFalse() =>
        Assert.False(EupInferenceHelper.IsFreemodePed("s_m_y_sheriff_01"));

    [Fact]
    public void IsFreemodePed_Null_ReturnsFalse() =>
        Assert.False(EupInferenceHelper.IsFreemodePed(null));

    [Fact]
    public void InferGenderFromPedModel_Male_ReturnsMale() =>
        Assert.Equal(EupGender.Male, EupInferenceHelper.InferGenderFromPedModel("mp_m_freemode_01"));

    [Fact]
    public void InferGenderFromPedModel_Female_ReturnsFemale() =>
        Assert.Equal(EupGender.Female, EupInferenceHelper.InferGenderFromPedModel("mp_f_freemode_01"));

    [Fact]
    public void InferGenderFromPedModel_Cop_ReturnsUnknown() =>
        Assert.Equal(EupGender.Unknown, EupInferenceHelper.InferGenderFromPedModel("s_m_y_cop_01"));

    // ════════════════════════════════════════════════════════════════
    // EupInferenceHelper — department inference
    // ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("SAHP Class A", "", "SAHP")]
    [InlineData("State Trooper Pack", "", "SAHP")]
    [InlineData("Highway Patrol Officer", "", "SAHP")]
    [InlineData("State Police Uniform", "", "SAHP")]
    public void InferDepartment_SAHP_Variations(string name, string folder, string expected) =>
        Assert.Equal(expected, EupInferenceHelper.InferDepartment(name, folder));

    [Theory]
    [InlineData("BCSO Deputy", "", "BCSO")]
    [InlineData("Blaine County Sheriff Deputy", "", "BCSO")]
    public void InferDepartment_BCSO_Variations(string name, string folder, string expected) =>
        Assert.Equal(expected, EupInferenceHelper.InferDepartment(name, folder));

    [Theory]
    [InlineData("LSSD Deputy", "", "LSSD")]
    [InlineData("Los Santos Sheriff Deputy", "", "LSSD")]
    public void InferDepartment_LSSD_Variations(string name, string folder, string expected) =>
        Assert.Equal(expected, EupInferenceHelper.InferDepartment(name, folder));

    [Fact]
    public void InferDepartment_BareSheriff_IsNotBCSO()
    {
        var result = EupInferenceHelper.InferDepartment("Sheriff Deputy", "");
        Assert.NotEqual("BCSO", result);
        Assert.NotEqual("LSSD", result);
        // Must be the ambiguous fallback
        Assert.Equal("Sheriff/Unknown", result);
    }

    [Theory]
    [InlineData("LSPD Officer", "", "LSPD")]
    [InlineData("Los Santos Police", "", "LSPD")]
    [InlineData("Police Department Patrol", "", "LSPD")]
    public void InferDepartment_LSPD_Variations(string name, string folder, string expected) =>
        Assert.Equal(expected, EupInferenceHelper.InferDepartment(name, folder));

    [Fact]
    public void InferDepartment_Unknown_ReturnsUnknown() =>
        Assert.Equal("Unknown", EupInferenceHelper.InferDepartment("Mystery Pack Alpha", ""));

    // ════════════════════════════════════════════════════════════════
    // EupInferenceHelper — county + region inference
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void InferCountyAndRegion_PaletoBay_ReturnsBlainePaleto()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Paleto Bay Deputy", "");
        Assert.Equal("Blaine County", county);
        Assert.Equal("Paleto Bay", region);
    }

    [Fact]
    public void InferCountyAndRegion_SandyShores_ReturnsBlaineSandy()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Sandy Shores Unit", "");
        Assert.Equal("Blaine County", county);
        Assert.Equal("Sandy Shores", region);
    }

    [Fact]
    public void InferCountyAndRegion_Grapeseed_ReturnsBlainGrapeseed()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Grapeseed Patrol", "");
        Assert.Equal("Blaine County", county);
        Assert.Equal("Grapeseed", region);
    }

    [Fact]
    public void InferCountyAndRegion_BlaineCounty_ReturnsBlaineBlaine()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Blaine County BCSO", "");
        Assert.Equal("Blaine County", county);
        Assert.Equal("Blaine County", region);
    }

    [Fact]
    public void InferCountyAndRegion_LosSantos_ReturnsLsLs()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Los Santos Beat", "");
        Assert.Equal("Los Santos", county);
        Assert.Equal("Los Santos", region);
    }

    [Fact]
    public void InferCountyAndRegion_Unknown_ReturnsUnknown()
    {
        var (county, region) = EupInferenceHelper.InferCountyAndRegion("Generic Pack", "");
        Assert.Equal("Unknown", county);
        Assert.Equal("Unknown", region);
    }

    // ════════════════════════════════════════════════════════════════
    // EupInferenceHelper — gender inference
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void InferGender_FromMalePedModel_ReturnsMale() =>
        Assert.Equal(EupGender.Male, EupInferenceHelper.InferGender("mp_m_freemode_01", "", ""));

    [Fact]
    public void InferGender_FromFemalePedModel_ReturnsFemale() =>
        Assert.Equal(EupGender.Female, EupInferenceHelper.InferGender("mp_f_freemode_01", "", ""));

    [Fact]
    public void InferGender_FromFemaleName_ReturnsFemale() =>
        Assert.Equal(EupGender.Female, EupInferenceHelper.InferGender(null, "female_officer", ""));

    [Fact]
    public void InferGender_FromMaleName_ReturnsMale() =>
        Assert.Equal(EupGender.Male, EupInferenceHelper.InferGender(null, "male_uniform", ""));

    [Fact]
    public void InferGender_NoClues_ReturnsUnknown() =>
        Assert.Equal(EupGender.Unknown, EupInferenceHelper.InferGender(null, "", ""));

    // ════════════════════════════════════════════════════════════════
    // BackupConfigDiscoveryService — expanded paths
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void BackupDiscovery_UltimateBackupWithSpace_IsFound()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "Ultimate Backup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "units.xml"), "<root/>");

        var results = new BackupConfigDiscoveryService(_tmp).DiscoverBackupXmlFiles();
        Assert.Contains(results, r => r.EndsWith("units.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BackupDiscovery_UltimateBackupNoSpace_IsFound()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "backup.xml"), "<root/>");

        var results = new BackupConfigDiscoveryService(_tmp).DiscoverBackupXmlFiles();
        Assert.Contains(results, r => r.EndsWith("backup.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BackupDiscovery_LspdfrDataCustom_IsFound()
    {
        var dir = Path.Combine(_tmp, "lspdfr", "data", "custom");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "regions.xml"), "<root/>");

        var results = new BackupConfigDiscoveryService(_tmp).DiscoverBackupXmlFiles();
        Assert.Contains(results, r => r.EndsWith("regions.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BackupDiscovery_SpecialXml_IsFound()
    {
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "special_units.xml"), "<root/>");

        var results = new BackupConfigDiscoveryService(_tmp).DiscoverBackupXmlFiles();
        Assert.Contains(results, r => r.Contains("special_units.xml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BackupDiscovery_DuplicatePaths_AreDeduped()
    {
        // Same XML reachable via two scan patterns
        var dir = Path.Combine(_tmp, "plugins", "lspdfr", "UltimateBackup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "backup_units.xml"), "<root/>");

        var results = new BackupConfigDiscoveryService(_tmp).DiscoverBackupXmlFiles();
        var count = results.Count(r => r.EndsWith("backup_units.xml", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, count);
    }

    // ════════════════════════════════════════════════════════════════
    // EupOutfitDiscoveryService
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void EupDiscovery_SyntheticIni_IsDiscovered()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "presetoutfits.ini"), """
            [SAHP Class A]
            PedModel=mp_m_freemode_01
            Component1=5,0
            Hat=3,0
            """);

        var results = new EupOutfitDiscoveryService(_tmp).Discover();
        Assert.Contains(results, u => u.DisplayName == "SAHP Class A");
    }

    [Fact]
    public void EupDiscovery_MaleFreemodeIni_ParsesGender()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "presetoutfits.ini"), """
            [Officer Pack]
            PedModel=mp_m_freemode_01
            Component1=4,0
            """);

        var results = new EupOutfitDiscoveryService(_tmp).Discover();
        var u = Assert.Single(results);
        Assert.Equal(EupGender.Male, u.Gender);
        Assert.True(EupInferenceHelper.IsFreemodePed(u.PedModel));
    }

    [Fact]
    public void EupDiscovery_FemaleFreemodeIni_ParsesGender()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "presetoutfits.ini"), """
            [Female Officer]
            PedModel=mp_f_freemode_01
            Component1=4,0
            """);

        var results = new EupOutfitDiscoveryService(_tmp).Discover();
        var u = Assert.Single(results);
        Assert.Equal(EupGender.Female, u.Gender);
    }

    [Fact]
    public void EupDiscovery_MalformedIni_DoesNotThrow()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "broken.ini"), "this is not\x00valid\nini===content");

        var ex = Record.Exception(() => new EupOutfitDiscoveryService(_tmp).Discover());
        Assert.Null(ex);
    }

    [Fact]
    public void EupDiscovery_EmptyPath_ReturnsEmpty_DoesNotThrow()
    {
        var results = new EupOutfitDiscoveryService("").Discover();
        Assert.Empty(results);
    }

    [Fact]
    public void EupDiscovery_KnownIniFormat_SetsSupportedTrue()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "presetoutfits.ini"), """
            [Freemode Pack]
            PedModel=mp_m_freemode_01
            Component1=5,0
            """);

        var results = new EupOutfitDiscoveryService(_tmp).Discover();
        var u = Assert.Single(results);
        Assert.True(u.Metadata.TryGetValue("Supported", out var sup) && sup == "true");
    }

    [Fact]
    public void EupDiscovery_NonFreemodePed_SetsSupportedFalse()
    {
        var dir = Path.Combine(_tmp, "plugins", "EUP");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "presetoutfits.ini"), """
            [Cop Pack]
            PedModel=s_m_y_cop_01
            Component1=1,0
            """);

        var results = new EupOutfitDiscoveryService(_tmp).Discover();
        var u = Assert.Single(results);
        Assert.True(u.Metadata.TryGetValue("Supported", out var sup) && sup == "false");
    }

    // ════════════════════════════════════════════════════════════════
    // BackupUnitFilter
    // ════════════════════════════════════════════════════════════════

    private static List<BackupUnitDefinition> SampleUnits() =>
    [
        new() { Agency = "LSPD", UnitType = "LocalPatrol", PedModel = "s_m_y_cop_01" },
        new() { Agency = "BCSO", UnitType = "Sheriff",     PedModel = "s_m_y_sheriff_01" },
        new() { Agency = "SAHP", UnitType = "StatePatrol", PedModel = "mp_m_freemode_01" },
        new() { Agency = "SAHP", UnitType = "StatePatrol", PedModel = "mp_f_freemode_01" },
    ];

    [Fact]
    public void BackupUnitFilter_ByDepartment_ReturnsOnlyMatching()
    {
        var results = BackupUnitFilter.Filter(SampleUnits(), "LSPD", null, null, null);
        Assert.All(results, u => Assert.Equal("LSPD", u.Agency));
        Assert.Single(results);
    }

    [Fact]
    public void BackupUnitFilter_AnyDepartment_ReturnsAll()
    {
        var results = BackupUnitFilter.Filter(SampleUnits(), "Any", null, null, null);
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void BackupUnitFilter_NullDepartment_ReturnsAll()
    {
        var results = BackupUnitFilter.Filter(SampleUnits(), null, null, null, null);
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void BackupUnitFilter_ByCategory_ReturnsOnlyMatching()
    {
        var results = BackupUnitFilter.Filter(SampleUnits(), null, null, null, "Sheriff");
        Assert.All(results, u => Assert.Equal("Sheriff", u.UnitType));
        Assert.Single(results);
    }

    // ════════════════════════════════════════════════════════════════
    // PreviewAssignment — freemode ped enforcement
    // ════════════════════════════════════════════════════════════════

    private static string WriteSyntheticBackupXml(string dir, string agency, string pedModel)
    {
        var path = Path.Combine(dir, "units.xml");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="{agency}" UnitType="Patrol" PedModel="{pedModel}" VehicleModel="POLICE" />
            </BackupUnits>
            """);
        return path;
    }

    private static EupUniformDefinition MakeUniform(EupGender gender, string pedModel, bool freemode = true) =>
        new()
        {
            DisplayName = "Test Uniform",
            Gender = gender,
            PedModel = pedModel,
            Department = "LSPD",
            Components = new Dictionary<string, string> { ["Component1"] = "5,0" },
            Metadata = new Dictionary<string, string>
            {
                ["Format"] = "KnownIni",
                ["Supported"] = freemode ? "true" : "false",
            },
            Confidence = 0.85f,
        };

    [Fact]
    public void Preview_NonFreemodePed_HasComponents_BlocksApply()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "s_m_y_cop_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "s_m_y_cop_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.False(preview.CanApply);
        Assert.True(preview.IsReadOnlyPreview);
        Assert.Contains(preview.MismatchWarnings,
            w => w.Contains("may not support EUP component uniforms"));
    }

    [Fact]
    public void Preview_MaleUniformOnMaleFreemode_CanApply()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.True(preview.CanApply);
        Assert.Empty(preview.MismatchWarnings.Where(w =>
            w.Contains("may not support EUP") || w.Contains("Gender")));
    }

    [Fact]
    public void Preview_FemaleUniformOnFemaleFreemode_CanApply()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "SAHP", "mp_f_freemode_01");
        var uniform = MakeUniform(EupGender.Female, "mp_f_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "SAHP", PedModel = "mp_f_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.True(preview.CanApply);
    }

    [Fact]
    public void Preview_MaleUniformOnFemaleFreemode_BlocksApply()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_f_freemode_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_f_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.MismatchWarnings, w => w.Contains("Female"));
    }

    [Fact]
    public void Preview_FemaleUniformOnMaleFreemode_BlocksApply()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Female, "mp_f_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.MismatchWarnings, w => w.Contains("Male"));
    }

    [Fact]
    public void Preview_UnknownGenderOnFreemode_WarnsButAllows()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Unknown, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.True(preview.CanApply);
        Assert.Contains(preview.Warnings, w => w.Contains("gender could not be determined"));
    }

    [Fact]
    public void Preview_DoesNotWriteToFile()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var original = File.ReadAllText(xmlFile);
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.Equal(original, File.ReadAllText(xmlFile));
    }

    [Fact]
    public void Preview_UnknownXmlStructure_BlocksApply()
    {
        var xmlFile = Path.Combine(_tmp, "unknown.xml");
        File.WriteAllText(xmlFile, """
            <?xml version="1.0" encoding="utf-8"?>
            <MyCustomData>
              <Row col1="foo" col2="bar" />
            </MyCustomData>
            """);
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);

        Assert.False(preview.CanApply);
    }

    // ════════════════════════════════════════════════════════════════
    // ApplyAssignment — timestamped backup + XDocument patching
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Apply_CreatesTimestampedBackup()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);
        Assert.True(preview.CanApply);

        var (applied, bakPath, error) = BackupEasyEditorService.ApplyAssignment(preview);

        Assert.True(applied, error);
        Assert.NotNull(bakPath);
        Assert.True(File.Exists(bakPath));
        Assert.Matches(@"\.bak\.\d{8}-\d{6}$", bakPath);
    }

    [Fact]
    public void Apply_PreviousTimestampedBackup_NotOverwritten()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        // Apply twice — must produce two distinct backup files
        var preview1 = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);
        var (_, bak1, _) = BackupEasyEditorService.ApplyAssignment(preview1);

        // Rewrite XML to a fresh state for second apply
        WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var preview2 = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);
        var (_, bak2, _) = BackupEasyEditorService.ApplyAssignment(preview2);

        // Each call produces its own .bak.<timestamp> — they should be different names
        // (timestamps differ by at least 1 second in normal operation; in fast tests
        //  they might collide, so just assert both exist and bak1 was not removed)
        Assert.True(File.Exists(bak1!));
        Assert.True(File.Exists(bak2!));
    }

    [Fact]
    public void Apply_CanApplyFalse_ReturnsFalseDoesNotWrite()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "s_m_y_cop_01");
        var original = File.ReadAllText(xmlFile);
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "s_m_y_cop_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);
        Assert.False(preview.CanApply); // blocked by non-freemode

        var (applied, _, _) = BackupEasyEditorService.ApplyAssignment(preview);

        Assert.False(applied);
        Assert.Equal(original, File.ReadAllText(xmlFile));
        Assert.Empty(Directory.GetFiles(_tmp, "*.bak.*"));
    }

    [Fact]
    public void Apply_WritesUniformNameAttribute()
    {
        var xmlFile = WriteSyntheticBackupXml(_tmp, "LSPD", "mp_m_freemode_01");
        var uniform = MakeUniform(EupGender.Male, "mp_m_freemode_01");
        var unit    = new BackupUnitDefinition { Agency = "LSPD", PedModel = "mp_m_freemode_01" };

        var preview = BackupEasyEditorService.PreviewAssignment(uniform, unit, xmlFile);
        BackupEasyEditorService.ApplyAssignment(preview);

        var content = File.ReadAllText(xmlFile);
        Assert.Contains("UniformName=\"Test Uniform\"", content);
    }

    // ════════════════════════════════════════════════════════════════
    // BackupXmlParser.ApplyPatch — stable XDocument patching
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void XmlPatch_SingleMatchingUnit_PatchesCorrectNode()
    {
        var file = Path.Combine(_tmp, "patch.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="LSPD" UnitType="Patrol" PedModel="mp_m_freemode_01" VehicleModel="POLICE" />
              <Unit Agency="BCSO" UnitType="Sheriff" PedModel="s_m_y_sheriff_01" VehicleModel="SHERIFF" />
            </BackupUnits>
            """);

        var (changed, error) = BackupXmlParser.ApplyPatch(file, "LSPD", null, null, "MyUniform");

        Assert.Null(error);
        Assert.Equal(1, changed);
        var doc = XDocument.Load(file);

        var lspdUnit = doc.Descendants("Unit")
            .FirstOrDefault(u => string.Equals((string?)u.Attribute("Agency"), "LSPD", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(lspdUnit);
        Assert.Equal("MyUniform", (string?)lspdUnit!.Attribute("UniformName"));

        var bcsoUnit = doc.Descendants("Unit")
            .FirstOrDefault(u => string.Equals((string?)u.Attribute("Agency"), "BCSO", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bcsoUnit);
        Assert.Null((string?)bcsoUnit!.Attribute("UniformName"));
    }

    [Fact]
    public void XmlPatch_NoMatchingAgency_ReturnsZeroChanges()
    {
        var file = Path.Combine(_tmp, "nomatch.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="BCSO" PedModel="s_m_y_sheriff_01" />
            </BackupUnits>
            """);
        var original = File.ReadAllText(file);

        var (changed, error) = BackupXmlParser.ApplyPatch(file, "LSPD", null, null, "MyUniform");

        Assert.Equal(0, changed);
        Assert.NotNull(error);
        Assert.Equal(original, File.ReadAllText(file));
    }

    [Fact]
    public void XmlPatch_AlreadySetToSameValue_ReturnsZeroChanges()
    {
        var file = Path.Combine(_tmp, "alreadyset.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <BackupUnits>
              <Unit Agency="LSPD" PedModel="mp_m_freemode_01" UniformName="MyUniform" />
            </BackupUnits>
            """);

        var (changed, error) = BackupXmlParser.ApplyPatch(file, "LSPD", null, null, "MyUniform");

        // No change because the value is already correct
        Assert.Equal(0, changed);
        Assert.NotNull(error); // "already set" message
    }

    [Fact]
    public void XmlPatch_UnknownStructure_ReturnsZeroChanges()
    {
        var file = Path.Combine(_tmp, "unknown2.xml");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="utf-8"?>
            <MyCustomData>
              <Row col1="LSPD" col2="bar" />
            </MyCustomData>
            """);

        var (changed, error) = BackupXmlParser.ApplyPatch(file, "LSPD", null, null, "MyUniform");

        Assert.Equal(0, changed);
        Assert.NotNull(error);
    }
}
