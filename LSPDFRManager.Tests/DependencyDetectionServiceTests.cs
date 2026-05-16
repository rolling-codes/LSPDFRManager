using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for DependencyDetectionService — the pure mapper from ModTypeDetectionResult
/// to dependency warnings.  Uses ModTypeDetectionService to produce realistic inputs.
/// </summary>
public class DependencyDetectionServiceTests
{
    private static readonly DependencyDetectionService Svc = new();
    private static readonly ModTypeDetectionService    TypeSvc = new();

    private static DependencyDetectionResult Detect(IReadOnlyList<string> entries, string? archiveName = null)
    {
        var modType = TypeSvc.Detect(entries, archiveName);
        return Svc.Detect(modType);
    }

    // ── Script ───────────────────────────────────────────────────────────────

    [Fact]
    public void Script_ProducesScriptHookVWarning()
    {
        var result = Detect(["scripts/myscript.cs"]);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, w => w.Name.Contains("Script Hook V"));
    }

    [Fact]
    public void Script_ProducesSHVDNWarning()
    {
        var result = Detect(["scripts/myscript.cs"]);

        Assert.Contains(result.Warnings, w => w.Name.Contains("ScriptHookVDotNet"));
    }

    [Fact]
    public void Script_SourceTypeIsScript()
    {
        var result = Detect(["scripts/myscript.cs"]);

        Assert.All(result.Warnings, w => Assert.Equal(ModType.Script, w.SourceType));
    }

    [Fact]
    public void Script_ReasonMentionsScriptType()
    {
        var result = Detect(["scripts/myscript.cs"]);

        Assert.All(result.Warnings, w =>
            Assert.Contains("script", w.Reason, StringComparison.OrdinalIgnoreCase));
    }

    // ── ASI Mod ──────────────────────────────────────────────────────────────

    [Fact]
    public void AsiMod_ProducesScriptHookVWarning()
    {
        var result = Detect(["scripthookv.asi"]);

        Assert.Contains(result.Warnings, w => w.Name.Contains("Script Hook V"));
    }

    [Fact]
    public void AsiMod_ProducesAsiLoaderWarning()
    {
        var result = Detect(["trainer.asi"]);

        Assert.Contains(result.Warnings, w => w.Name.Contains("ASI Loader"));
    }

    [Fact]
    public void AsiMod_SourceTypeIsAsi()
    {
        var result = Detect(["trainer.asi"]);

        Assert.All(result.Warnings, w => Assert.Equal(ModType.AsiMod, w.SourceType));
    }

    // ── LSPDFR Plugin ────────────────────────────────────────────────────────

    [Fact]
    public void LspdfrPlugin_ProducesLspdfrWarning()
    {
        var result = Detect(["plugins/lspdfr/myplugin.dll"]);

        Assert.Contains(result.Warnings, w => w.Name.Contains("LSPDFR"));
    }

    [Fact]
    public void LspdfrPlugin_ProducesRagePluginHookWarning()
    {
        var result = Detect(["plugins/lspdfr/myplugin.dll"]);

        Assert.Contains(result.Warnings, w => w.Name.Contains("RAGE Plugin Hook"));
    }

    [Fact]
    public void LspdfrPlugin_SourceTypeIsPlugin()
    {
        var result = Detect(["plugins/lspdfr/myplugin.dll"]);

        Assert.All(result.Warnings, w => Assert.Equal(ModType.LspdfrPlugin, w.SourceType));
    }

    // ── OIV Package ──────────────────────────────────────────────────────────

    [Fact]
    public void OivPackage_ProducesOpenIvWarning()
    {
        var result = Detect(["assembly.xml", "content/update.rpf"]);

        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, w => w.Name.Contains("OpenIV"));
    }

    [Fact]
    public void OivPackage_ReasonMentionsManualInstall()
    {
        var result = Detect(["assembly.xml", "content/update.rpf"]);

        var oivWarning = result.Warnings.First(w => w.Name.Contains("OpenIV"));
        Assert.Contains("manually", oivWarning.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── Types with no dependencies (DLC, Map, Sound, Config) ─────────────────

    [Fact]
    public void DlcPack_NoWarnings()
    {
        var result = Detect(["mods/update/x64/dlcpacks/myaddon/dlc.rpf"]);

        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void MapMlo_NoWarnings()
    {
        var result = Detect(["mymap.ymap", "mymap.ytyp"]);

        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void SoundPack_NoWarnings()
    {
        var result = Detect(["x64/audio/sfx/sirens.awc"]);

        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void ConfigOnly_NoWarnings()
    {
        var result = Detect(["settings.ini", "config.xml"]);

        Assert.False(result.HasWarnings);
    }

    // ── Unknown ──────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_NoWarnings()
    {
        var modType = new ModTypeDetectionResult
        {
            PrimaryType = ModType.Unknown,
            Confidence  = 0f,
        };

        var result = Svc.Detect(modType);

        Assert.False(result.HasWarnings);
    }

    // ── Mixed archives — deduplication ────────────────────────────────────────

    [Fact]
    public void Mixed_AsiAndScript_ScriptHookVNotDuplicated()
    {
        // Both ASI and Script require Script Hook V — it must appear exactly once.
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        var shvCount = result.Warnings.Count(w => w.Name.Contains("Script Hook V")
                                                  && !w.Name.Contains("DotNet"));
        Assert.Equal(1, shvCount);
    }

    [Fact]
    public void Mixed_AsiAndScript_AllDepsPresent()
    {
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        var names = result.Warnings.Select(w => w.Name).ToList();
        Assert.Contains(names, n => n.Contains("Script Hook V") && !n.Contains("DotNet"));
        Assert.Contains(names, n => n.Contains("ASI Loader"));
        Assert.Contains(names, n => n.Contains("ScriptHookVDotNet"));
    }

    [Fact]
    public void Mixed_DepCountNeverExceedsUniqueDependencies()
    {
        // Worst-case mixed: ASI + Script.  Max unique deps = 3 (SHV, ASI Loader, SHVDN).
        var result = Detect(["trainer.asi", "scripts/helper.cs"]);

        var uniqueNames = result.Warnings.Select(w => w.Name)
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .Count();
        Assert.Equal(result.Warnings.Count, uniqueNames);
    }

    // ── Evidence preservation through the chain ───────────────────────────────

    [Fact]
    public void EvidenceChain_WarningNamesAreNonEmpty()
    {
        var cases = new[]
        {
            Detect(["trainer.asi"]),
            Detect(["scripts/mod.cs"]),
            Detect(["plugins/lspdfr/plugin.dll"]),
            Detect(["assembly.xml"]),
        };

        foreach (var result in cases)
        foreach (var warning in result.Warnings)
        {
            Assert.False(string.IsNullOrWhiteSpace(warning.Name));
            Assert.False(string.IsNullOrWhiteSpace(warning.Reason));
        }
    }

    [Fact]
    public void EvidenceChain_SourceTypeMatchesExpectedCategory()
    {
        // Verify that warnings are attributed to the correct originating type.
        var scriptResult = Detect(["scripts/mod.cs"]);
        Assert.All(scriptResult.Warnings, w => Assert.Equal(ModType.Script, w.SourceType));

        var pluginResult = Detect(["plugins/lspdfr/plugin.dll"]);
        Assert.All(pluginResult.Warnings, w => Assert.Equal(ModType.LspdfrPlugin, w.SourceType));
    }

    // ── Planner integration — warnings appear in the review plan ─────────────

    [Fact]
    public void PlannerIntegration_ScriptArchive_WarningsInPlan()
    {
        // Build a real InstallPlan from a temp ZIP containing a script in scripts/.
        var tempDir = Path.Combine(Path.GetTempPath(), $"dep_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "myscript.zip");

        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("scripts/myscript.cs");
                using var w = new StreamWriter(entry.Open());
                w.Write("// placeholder script");
            }

            var plan = new SmartInstallPlanner().BuildPlan(zipPath);

            Assert.Contains(plan.Warnings, w => w.Contains("Script Hook V"));
            Assert.Contains(plan.Warnings, w => w.Contains("ScriptHookVDotNet"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PlannerIntegration_AsiArchive_WarningsInPlan()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dep_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "trainer.zip");

        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("trainer.asi");
                using var w = entry.Open();
                w.Write(new byte[] { 0x4D, 0x5A }); // MZ header stub
            }

            var plan = new SmartInstallPlanner().BuildPlan(zipPath);

            Assert.Contains(plan.Warnings, w => w.Contains("Script Hook V"));
            Assert.Contains(plan.Warnings, w => w.Contains("ASI Loader"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PlannerIntegration_DlcArchive_NoDependencyWarnings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dep_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "addoncar.zip");

        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("mods/update/x64/dlcpacks/myaddon/dlc.rpf");
                using var w = entry.Open();
                w.Write(new byte[] { 0x00 });
            }

            var plan = new SmartInstallPlanner().BuildPlan(zipPath);

            Assert.DoesNotContain(plan.Warnings, w => w.StartsWith("Dependency:"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
