using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.SafeMode;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests.Features.SafeMode;

/// <summary>
/// Integration-style tests for SafeModeController.
/// All tests use temp directories — no real GTA V install required.
/// </summary>
[Collection("CommandCenter")]
public class SafeModeControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gtaDir;
    private readonly string _appDataDir;

    public SafeModeControllerTests()
    {
        _tempDir    = Path.Combine(Path.GetTempPath(), $"sm_{Guid.NewGuid():N}");
        _gtaDir     = Path.Combine(_tempDir, "GTA5");
        _appDataDir = Path.Combine(_tempDir, "AppData");

        Directory.CreateDirectory(_gtaDir);
        Directory.CreateDirectory(_appDataDir);

        AppDataPaths.OverrideRoot(_appDataDir);
        AppConfig.Instance.GtaPath    = _gtaDir;
        AppConfig.Instance.BackupPath = Path.Combine(_appDataDir, "Backups");
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string PlacePlugin(string relativePath)
    {
        var full = Path.Combine(_gtaDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "data");
        return full;
    }

    private SafeModeController MakeController(RestorePointService? rp = null)
        => new(manager: new SafeLaunchManager(), restorePoints: rp ?? new RestorePointService());

    // ── Preview: pure, no side-effects ────────────────────────────────────────

    [Fact]
    public async Task Preview_ReturnsChanges_WithoutModifyingFiles()
    {
        PlacePlugin(@"plugins\lspdfr\SomeMod.dll");

        var ctrl = MakeController();
        var plan = await ctrl.BuildPreviewAsync("LspdfrOnly");

        Assert.NotEmpty(plan.Changes);
        // File must still exist unchanged after preview
        Assert.True(File.Exists(Path.Combine(_gtaDir, @"plugins\lspdfr\SomeMod.dll")));
        Assert.False(File.Exists(Path.Combine(_gtaDir, @"plugins\lspdfr\SomeMod.dll.disabled")));
    }

    [Fact]
    public async Task Preview_EmptyWhenNoMatchingFiles()
    {
        var ctrl = MakeController();
        var plan = await ctrl.BuildPreviewAsync("LspdfrOnly");

        Assert.Empty(plan.Changes);
    }

    [Fact]
    public async Task Preview_LogsDiagnostic()
    {
        var ctrl = MakeController();
        AppLogger.Entries.Clear();

        await ctrl.BuildPreviewAsync("LspdfrOnly");

        Assert.Contains(AppLogger.Entries, e => e.Message.Contains("[SafeMode]") && e.Message.Contains("Preview"));
    }

    // ── Apply: backup-first requirement ───────────────────────────────────────

    [Fact]
    public async Task Apply_BackupFail_NoFilesModified()
    {
        var pluginPath = PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        var ctrl = MakeController(rp: new FailingRestorePointService());
        var result = await ctrl.ApplyAsync(plan);

        Assert.False(result.Success);
        Assert.Equal(0, result.FilesDisabled);
        // File must be untouched
        Assert.True(File.Exists(pluginPath));
        Assert.False(File.Exists(pluginPath + ".disabled"));
    }

    [Fact]
    public async Task Apply_BackupFail_ResultMessageIsFriendly()
    {
        PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        var ctrl   = MakeController(rp: new FailingRestorePointService());
        var result = await ctrl.ApplyAsync(plan);

        Assert.Contains("Backup failed", result.StatusMessage);
        Assert.DoesNotContain("FailingRestorePointService", result.StatusMessage);
        Assert.DoesNotContain("Exception", result.StatusMessage);
    }

    // ── Apply: happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_DisablesExpectedFiles()
    {
        var pluginPath = PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        var ctrl   = MakeController();
        var result = await ctrl.ApplyAsync(plan);

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesDisabled);
        Assert.True(File.Exists(pluginPath + ".disabled"));
        Assert.False(File.Exists(pluginPath));
    }

    [Fact]
    public async Task Apply_LogsDiagnosticsOnSuccess()
    {
        PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        AppLogger.Entries.Clear();
        var ctrl = MakeController();
        await ctrl.ApplyAsync(plan);

        Assert.Contains(AppLogger.Entries, e => e.Message.Contains("[SafeMode]") && e.Message.Contains("Applying"));
        Assert.Contains(AppLogger.Entries, e => e.Message.Contains("[SafeMode]") && e.Message.Contains("Result"));
    }

    // ── Verification ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_VerifiesDisabledState_SuccessWhenConfirmed()
    {
        PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        var ctrl   = MakeController();
        var result = await ctrl.ApplyAsync(plan);

        Assert.True(result.Success);
        Assert.DoesNotContain("Verification failed", result.StatusMessage);
    }

    [Fact]
    public async Task Apply_VerificationMismatch_IsUserSafe_AndLogged()
    {
        // Plant a file in the plan but don't actually put it on disk, so Move fails
        // and verification detects the mismatch via a custom plan with a non-existent file.
        var fakePath = Path.Combine(_gtaDir, "plugins", "lspdfr", "Ghost.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(fakePath)!);

        var plan = new SafeLaunchPlan
        {
            Mode = "LspdfrOnly",
            Changes =
            [
                new SafeLaunchChange { FilePath = fakePath, WasEnabled = true, WillBeEnabled = false },
            ],
        };

        AppLogger.Entries.Clear();
        var ctrl   = MakeController();
        var result = await ctrl.ApplyAsync(plan);

        // File never existed so Move fails → failedPaths contains it
        // OR file was never created → verify mismatch
        // Either way: result is not a raw exception
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.StatusMessage));
        // Message must not contain raw exception type names
        Assert.DoesNotContain("FileNotFoundException", result.StatusMessage);
        Assert.DoesNotContain("IOException", result.StatusMessage);
        // Diagnostics logged
        Assert.Contains(AppLogger.Entries, e => e.Message.Contains("[SafeMode]"));
    }

    // ── DisableRecentMods mode ────────────────────────────────────────────────

    [Fact]
    public async Task Preview_DisableRecentMods_SelectsOnlyRecentFiles()
    {
        var scriptsDir = Path.Combine(_gtaDir, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var recentFile = Path.Combine(scriptsDir, "New.cs");
        File.WriteAllText(recentFile, "data");
        // Old file: backdate via creation time workaround (temp copy trick)
        var oldFile = Path.Combine(scriptsDir, "Old.cs");
        File.WriteAllText(oldFile, "data");
        File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-30));

        var ctrl = MakeController();
        var plan = await ctrl.BuildPreviewAsync("DisableRecentMods");

        Assert.Contains(plan.Changes, c => c.FilePath == recentFile);
        Assert.DoesNotContain(plan.Changes, c => c.FilePath == oldFile);
    }

    // ── Cancel from preview ───────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_FromPreview_LeavesFilesystemUnchanged()
    {
        var pluginPath = PlacePlugin(@"plugins\lspdfr\SomeMod.dll");

        var ctrl = MakeController();
        // Build preview (read-only)
        var plan = await ctrl.BuildPreviewAsync("LspdfrOnly");

        // Do NOT call ApplyAsync — simulate user hitting Cancel
        Assert.True(File.Exists(pluginPath));
        Assert.False(File.Exists(pluginPath + ".disabled"));
    }

    // ── Progress reporting ────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_ReportsProgressMessages()
    {
        PlacePlugin(@"plugins\lspdfr\SomeMod.dll");
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        var messages = new List<string>();
        var progress = new Progress<string>(m => messages.Add(m));

        var ctrl = MakeController();
        await ctrl.ApplyAsync(plan, progress);

        Assert.NotEmpty(messages);
        Assert.Contains(messages, m => m.Contains("Restore point") || m.Contains("Disabled") || m.Contains("Applying"));
    }
}

/// <summary>Simulates a backup service that always fails to save.</summary>
file sealed class FailingRestorePointService : RestorePointService
{
    public override Task SaveAsync(RestorePoint point)
        => throw new IOException("Simulated backup failure");
}
