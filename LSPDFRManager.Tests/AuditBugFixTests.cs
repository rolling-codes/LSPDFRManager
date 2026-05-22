using LSPDFRManager.Domain;
using LSPDFRManager.Features.Install;
using LSPDFRManager.Services;
using Xunit;
using System.Collections.Generic;

namespace LSPDFRManager.Tests;

/// <summary>
/// TDD red-phase tests for the 10-item audit checklist
/// (items 5, 7, 9, 10 have a testable service-layer surface from this project).
/// </summary>
[Collection("AppData serial")]
public class AuditBugFixTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lsp_audit_{Guid.NewGuid():N}");

    public AuditBugFixTests()
    {
        Directory.CreateDirectory(_tempRoot);
        var appDataDir = Path.Combine(_tempRoot, "AppData");
        var gtaDir     = Path.Combine(_tempRoot, "GTA5");
        Directory.CreateDirectory(appDataDir);
        Directory.CreateDirectory(gtaDir);

        AppDataPaths.OverrideRoot(appDataDir);
        AppConfig.Instance.GtaPath    = gtaDir;
        AppConfig.Instance.BackupPath = Path.Combine(appDataDir, "Backups");
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch { }
    }

    // ── Item 7: RestorePointService auto-loads in constructor ─────────────────

    [Fact]
    public async Task RestorePointService_NewInstance_AutoLoadsPersistedPoints()
    {
        Directory.CreateDirectory(AppDataPaths.RestorePointsDirectory);

        // Arrange: persist one restore point
        var svc1 = new RestorePointService();
        var rp = new RestorePoint { OperationName = "Persisted Op" };
        await svc1.SaveAsync(rp);

        // Act: create a brand-new instance — must NOT need an explicit Load() call
        var svc2 = new RestorePointService();

        // Assert: the persisted point must be immediately visible
        Assert.Contains(svc2.Points, p => p.OperationName == "Persisted Op");
    }

    // ── Item 5: DetectBatchAsync propagates OperationCanceledException ─────────

    [Fact]
    public async Task DetectBatch_WhenTokenPreCancelled_ThrowsOperationCancelled()
    {
        var controller = new InstallWorkflowController();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Before fix: OCE is swallowed and an empty list is returned.
        // After fix: OCE propagates out of the method.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.DetectBatchAsync(new[] { Path.Combine(_tempRoot, "nonexistent.zip") }, cts.Token));
    }

    // ── Item 10: ChangeHistoryService.ExportAsync sanitizes AffectedFile ──────

    [Fact]
    public async Task ChangeHistoryExport_Json_SanitizesAffectedFilePaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rawPath = Path.Combine(appData, "LSPDFRManager", "SomeMod.dll");

        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Installed SomeMod", affectedFile: rawPath);

        var exportPath = Path.Combine(_tempRoot, "history.json");
        await svc.ExportAsync(exportPath, asJson: true);

        // Deserialize back to check the actual string values (avoids JSON escape confusion)
        var content = File.ReadAllText(exportPath);
        var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var entries = JsonSerializer.Deserialize<List<ChangeHistoryEntry>>(content, opts)!;

        // After the fix, the raw AppData path must not appear in any AffectedFile field
        Assert.All(entries, e =>
        {
            if (e.AffectedFile is not null)
                Assert.DoesNotContain(appData, e.AffectedFile, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Item 9: BatchReinstallService validates SourceArchivePath extension ────

    [Fact]
    public async Task BatchReinstall_ExeSourcePath_IsSkippedWithIssue()
    {
        // Use cmd.exe (always present on Windows) as a dangerous non-archive path
        var exePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

        if (!File.Exists(exePath))
            return; // Guard: skip on environments without cmd.exe

        // Construct via domain types so ModType serializes as integer (System.Text.Json default)
        var manifest = new ModManifest
        {
            Mods = new List<ManifestEntry>
            {
                new ManifestEntry
                {
                    Name              = "EvilMod",
                    Type              = ModType.Script,
                    SourceArchivePath = exePath,
                },
            },
        };

        var manifestPath = Path.Combine(_tempRoot, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var svc = new BatchReinstallService();
        var issues = await svc.ReinstallFromManifestAsync(manifestPath);

        // Before fix: issues is empty (the exe is enqueued and fails silently in the install worker)
        // After fix:  the manifest entry is rejected by extension validation and added to issues
        Assert.Contains(issues, i => i.Contains("EvilMod", StringComparison.OrdinalIgnoreCase));
    }
}
