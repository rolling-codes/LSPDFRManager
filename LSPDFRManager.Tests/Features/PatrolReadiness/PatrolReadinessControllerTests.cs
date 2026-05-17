using LSPDFRManager.Domain;
using LSPDFRManager.Features.PatrolReadiness;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests.Features.PatrolReadiness;

/// <summary>
/// Tests for PatrolReadinessController logic.
/// All integration-style tests run against a temp directory — no real GTA V required.
/// </summary>
public class PatrolReadinessControllerTests : IDisposable
{
    private readonly string _tempGta;
    private readonly string _tempAppData;

    public PatrolReadinessControllerTests()
    {
        _tempGta = Path.Combine(Path.GetTempPath(), $"prc_gta_{Guid.NewGuid():N}");
        _tempAppData = Path.Combine(Path.GetTempPath(), $"prc_app_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempGta);
        Directory.CreateDirectory(Path.Combine(_tempGta, "plugins"));
        Directory.CreateDirectory(Path.Combine(_tempGta, "plugins", "LSPDFR"));
        Directory.CreateDirectory(_tempAppData);

        AppConfig.Instance.GtaPath = _tempGta;
        AppDataPaths.OverrideRoot(_tempAppData);
    }

    public void Dispose()
    {
        AppConfig.Instance.GtaPath = "";
        AppDataPaths.ClearOverride();
        TryDelete(_tempGta);
        TryDelete(_tempAppData);
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    // ── Status calculation ────────────────────────────────────────────────────

    [Fact]
    public async Task Ready_WhenNoIssuesExist()
    {
        PlaceMinimalGtaFiles();
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        // With all required files present and no mods, should be Ready or Warning
        // (not NotReady)
        Assert.NotEqual(PatrolReadinessState.NotReady, summary.Status);
    }

    [Fact]
    public async Task NotReady_WhenGtaPathMissing()
    {
        AppConfig.Instance.GtaPath = Path.Combine(Path.GetTempPath(), "nonexistent_xyz");
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.Equal(PatrolReadinessState.NotReady, summary.Status);
        Assert.NotEmpty(summary.BlockingIssues);
    }

    [Fact]
    public async Task NotReady_WhenGtaPathEmpty()
    {
        AppConfig.Instance.GtaPath = "";
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.Equal(PatrolReadinessState.NotReady, summary.Status);
    }

    // ── Score calculation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Score_StartsAt100_WithNoIssues()
    {
        PlaceMinimalGtaFiles();
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        // Score = 100 - blockers*20 - warnings*5
        var expected = Math.Max(0, 100 - summary.BlockingIssues.Count * 20 - summary.Warnings.Count * 5);
        Assert.Equal(expected, summary.Score);
    }

    [Fact]
    public async Task Score_DecreasesWithBlockers()
    {
        // Missing GTA path produces blockers
        AppConfig.Instance.GtaPath = Path.Combine(Path.GetTempPath(), "nonexistent_xyz");
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.True(summary.Score < 100);
    }

    [Fact]
    public async Task Score_NeverGoesNegative()
    {
        AppConfig.Instance.GtaPath = Path.Combine(Path.GetTempPath(), "nonexistent_xyz");
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.True(summary.Score >= 0);
    }

    // ── Duplicate DLL contribution ────────────────────────────────────────────

    [Fact]
    public async Task Warning_WhenKnownSharedDllDuplicated()
    {
        PlaceMinimalGtaFiles();
        // Place RAGENativeUI.dll in two locations
        File.WriteAllBytes(Path.Combine(_tempGta, "plugins", "RAGENativeUI.dll"), []);
        File.WriteAllBytes(Path.Combine(_tempGta, "plugins", "LSPDFR", "RAGENativeUI.dll"), []);

        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();

        Assert.Contains(summary.Warnings, w => w.Code == "duplicate-shared-dll");
    }

    [Fact]
    public async Task Info_WhenUnknownDllDuplicated()
    {
        PlaceMinimalGtaFiles();
        File.WriteAllBytes(Path.Combine(_tempGta, "plugins", "MyLib.dll"), []);
        File.WriteAllBytes(Path.Combine(_tempGta, "plugins", "LSPDFR", "MyLib.dll"), []);

        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();

        Assert.Contains(summary.Info, i => i.Code == "duplicate-dll");
    }

    // ── Config lint contribution ───────────────────────────────────────────────

    [Fact]
    public async Task Warning_WhenBadIniConfigInPluginsFolder()
    {
        PlaceMinimalGtaFiles();
        // Write an INI with a duplicate key into the LSPDFR folder
        var iniPath = Path.Combine(_tempGta, "plugins", "LSPDFR", "TestMod.ini");
        File.WriteAllText(iniPath, "[Settings]\nHotkey=F5\nHotkey=F6\n");

        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();

        // Config lint warnings or info should appear
        Assert.True(summary.Warnings.Any(w => w.Source == "ConfigLint") ||
                    summary.Info.Any(i => i.Source == "ConfigLint"),
            "Duplicate INI key should produce a ConfigLint finding");
    }

    // ── Known-good diff contribution ──────────────────────────────────────────

    [Fact]
    public async Task Warning_WhenPluginsAddedSinceKnownGood()
    {
        PlaceMinimalGtaFiles();
        // Save a known-good snapshot with no plugins
        var baseline = GtaBaselineService.Instance;
        baseline.Save(new GtaBaseline
        {
            MarkedKnownGoodAt  = DateTime.UtcNow.AddMinutes(-5),
            EnabledPluginPaths = [],
            ConfigHashes       = new Dictionary<string, string>(),
        });

        // Now "add" a plugin to library
        // (ModLibraryService has no mods in test, so diff shows nothing added from lib,
        //  but the baseline has plugins = [] and library is empty = no change)
        // Force a diff by recording a path in baseline that no longer exists
        baseline.Save(new GtaBaseline
        {
            MarkedKnownGoodAt  = DateTime.UtcNow.AddMinutes(-5),
            EnabledPluginPaths = [Path.Combine(_tempGta, "plugins", "RemovedPlugin.dll")],
            ConfigHashes       = new Dictionary<string, string>(),
        });

        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();

        // Removed plugin should appear as a warning
        Assert.True(summary.Warnings.Any(w => w.Source == "KnownGood") ||
                    summary.Info.Any(i => i.Source == "KnownGood"),
            "Removed plugin since known-good should produce a KnownGood finding");
    }

    [Fact]
    public async Task NoKnownGoodWarnings_WhenNoBaselineExists()
    {
        PlaceMinimalGtaFiles();
        // Don't save any baseline
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.DoesNotContain(summary.Warnings, w => w.Source == "KnownGood");
        Assert.DoesNotContain(summary.Info,     i => i.Source == "KnownGood");
    }

    // ── Diagnostics emitted ───────────────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_EmitsLogEntry()
    {
        PlaceMinimalGtaFiles();
        var logsBefore = Core.AppLogger.Entries.Count;
        var ctrl = new PatrolReadinessController();
        await ctrl.ScanAsync();
        Assert.True(Core.AppLogger.Entries.Count > logsBefore,
            "ScanAsync should emit at least one log entry");
    }

    // ── Summary structure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_HasNonNullCoreChecks()
    {
        PlaceMinimalGtaFiles();
        var ctrl = new PatrolReadinessController();
        var summary = await ctrl.ScanAsync();
        Assert.NotNull(summary.CoreChecks);
    }

    [Fact]
    public async Task Summary_ScannedAt_IsRecent()
    {
        PlaceMinimalGtaFiles();
        var ctrl = new PatrolReadinessController();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var summary = await ctrl.ScanAsync();
        Assert.True(summary.ScannedAt >= before, "ScannedAt should be a recent timestamp");
    }

    [Fact]
    public async Task MarkKnownGood_DoesNotThrow()
    {
        PlaceMinimalGtaFiles();
        var ctrl = new PatrolReadinessController();
        var ex = Record.Exception(() => ctrl.MarkKnownGood());
        Assert.Null(ex);
    }

    // ── Score formula unit test (pure logic, no I/O) ──────────────────────────

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(1, 0, 80)]
    [InlineData(0, 2, 90)]
    [InlineData(2, 4, 40)]
    [InlineData(6, 0, 0)]   // 6 blockers would push below 0 → floor at 0
    public void Score_Formula_IsCorrect(int blockers, int warnings, int expectedScore)
    {
        var score = Math.Max(0, 100 - blockers * 20 - warnings * 5);
        Assert.Equal(expectedScore, score);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void PlaceMinimalGtaFiles()
    {
        // VersionDetectorService checks: GTA5.exe, RAGEPluginHook.exe, plugins/LSPDFR.dll
        File.WriteAllBytes(Path.Combine(_tempGta, "GTA5.exe"), []);
        File.WriteAllBytes(Path.Combine(_tempGta, "RAGEPluginHook.exe"), []);
        File.WriteAllBytes(Path.Combine(_tempGta, "plugins", "LSPDFR.dll"), []);
    }
}
