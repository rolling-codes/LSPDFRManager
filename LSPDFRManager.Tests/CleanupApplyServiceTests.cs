using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("CommandCenter")]
public class CleanupApplyServiceTests : CommandCenterTestBase
{
    // Test 13 — backup failure aborts with zero deletions
    [Fact]
    public async Task Apply_BackupFails_ZeroDeletions()
    {
        var filePath = Path.Combine(GtaDir, "RAGEPluginHook.exe");
        File.WriteAllText(filePath, "exe");

        var candidate = new RemovalCandidate
        {
            RelativePath = "RAGEPluginHook.exe",
            FullPath = filePath,
            Classification = CandidateClassification.RphCore,
            RiskLevel = CleanupRiskLevel.Low,
            Reason = "test",
        };

        // Use a backup service that always fails
        var backupService = new AlwaysFailingBackupService();
        var applyService = new CleanupApplyService(backupService);

        var result = await applyService.ApplyAsync(GtaDir, [candidate], CleanupMode.SafeCoreReset);

        Assert.False(result.Success);
        Assert.Empty(result.DeletedPaths);
        Assert.NotNull(result.AbortReason);
        // File must still exist — zero deletions
        Assert.True(File.Exists(filePath), "File must not be deleted when backup fails.");
    }

    [Fact]
    public async Task Apply_BlockedCandidate_AbortsBeforeBackup()
    {
        var filePath = Path.Combine(GtaDir, "GTA5.exe");
        File.WriteAllText(filePath, "exe");

        var candidate = new RemovalCandidate
        {
            RelativePath = "GTA5.exe",
            FullPath = filePath,
            Classification = CandidateClassification.Blocked,
            RiskLevel = CleanupRiskLevel.Advanced,
            Reason = "Blocked: GTA executable",
        };

        var applyService = new CleanupApplyService();
        var result = await applyService.ApplyAsync(GtaDir, [candidate], CleanupMode.SafeCoreReset);

        Assert.False(result.Success);
        Assert.NotNull(result.AbortReason);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task Apply_SuccessfulDelete_ReturnsSuccessAndDeletedPath()
    {
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        var filePath = Path.Combine(pluginsDir, "LSPD First Response.dll");
        File.WriteAllText(filePath, "dll");

        var candidate = new RemovalCandidate
        {
            RelativePath = Path.Combine("plugins", "LSPD First Response.dll"),
            FullPath = filePath,
            Classification = CandidateClassification.LspdfrCore,
            RiskLevel = CleanupRiskLevel.Low,
            Reason = "test",
        };

        var applyService = new CleanupApplyService();
        var result = await applyService.ApplyAsync(GtaDir, [candidate], CleanupMode.SafeCoreReset);

        Assert.True(result.Success);
        Assert.NotEmpty(result.DeletedPaths);
        Assert.False(File.Exists(filePath));
        Assert.NotNull(result.BackupZipPath);
    }

    [Fact]
    public async Task Apply_OutsideRootCandidate_AbortsWithZeroDeletions()
    {
        var outsidePath = Path.Combine(TempDir, "outside.dll");
        File.WriteAllText(outsidePath, "dll");

        var candidate = new RemovalCandidate
        {
            RelativePath = "outside.dll",
            FullPath = outsidePath,
            Classification = CandidateClassification.ManualReview,
            RiskLevel = CleanupRiskLevel.Advanced,
            Reason = "test",
        };

        var applyService = new CleanupApplyService();
        var result = await applyService.ApplyAsync(GtaDir, [candidate], CleanupMode.SafeCoreReset);

        Assert.False(result.Success);
        Assert.NotNull(result.AbortReason);
        Assert.True(File.Exists(outsidePath));
    }
}

/// <summary>Stub backup service that always reports failure — used for abort safety tests.</summary>
internal sealed class AlwaysFailingBackupService : GtaFileBackupService
{
    public override Task<CleanupBackupResult> CreateCleanupBackupAsync(
        string gtaRoot,
        IReadOnlyList<RemovalCandidate> selectedCandidates,
        CleanupMode mode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CleanupBackupResult
        {
            Success = false,
            FailedPaths = ["simulated-failure.zip"],
            ErrorMessage = "Simulated backup failure.",
        });
    }
}
