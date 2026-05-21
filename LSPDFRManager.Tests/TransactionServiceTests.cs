using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="TransactionService"/>: rollback correctness, persistence, and edge cases.
/// Inherits <see cref="CommandCenterTestBase"/> for isolated AppData/GTA directories.
/// </summary>
public class TransactionServiceTests : CommandCenterTestBase
{
    public TransactionServiceTests()
    {
        // Reload the singleton from the overridden AppData path set up by base class
        TransactionService.Instance.Reset();
    }

    // ── Rollback: added files ──────────────────────────────────────────────

    [Fact]
    public void Rollback_SkipsAddedFile_WhenHashDiffers()
    {
        var filePath = Path.Combine(TempDir, "plugin.dll");
        File.WriteAllText(filePath, "original content");

        // Record the hash of the original content
        var originalHash = ComputeHash(filePath);

        // Simulate the user modifying the file after install
        File.WriteAllText(filePath, "modified by user");

        var transaction = CommittedTransaction(filePath, installedHash: originalHash);
        TransactionService.Instance.Add(transaction);

        var result = TransactionService.Instance.Rollback(transaction.Id);

        Assert.False(result.IsComplete);
        Assert.True(result.IsPartial);
        Assert.Contains(filePath, result.SkippedFiles);
        Assert.True(File.Exists(filePath), "Modified file must not be deleted");
    }

    [Fact]
    public void Rollback_DeletesAddedFile_WhenHashMatches()
    {
        var filePath = Path.Combine(TempDir, "plugin.dll");
        File.WriteAllText(filePath, "installed content");
        var hash = ComputeHash(filePath);

        var transaction = CommittedTransaction(filePath, installedHash: hash);
        TransactionService.Instance.Add(transaction);

        var result = TransactionService.Instance.Rollback(transaction.Id);

        Assert.True(result.IsComplete);
        Assert.Empty(result.SkippedFiles);
        Assert.Empty(result.FailedFiles);
        Assert.False(File.Exists(filePath), "Unchanged installed file must be deleted on rollback");
    }

    [Fact]
    public void Rollback_SkipsAddedFile_WhenInstalledHashIsNull()
    {
        // Files >50 MB are not hashed at install time; InstalledHash is null.
        // Rollback must treat unknown state as unsafe and skip deletion.
        var filePath = Path.Combine(TempDir, "big_texture.yft");
        File.WriteAllText(filePath, "large file content");

        var transaction = CommittedTransaction(filePath, installedHash: null);
        TransactionService.Instance.Add(transaction);

        var result = TransactionService.Instance.Rollback(transaction.Id);

        Assert.False(result.IsComplete);
        Assert.True(result.IsPartial);
        Assert.Contains(filePath, result.SkippedFiles);
        Assert.True(File.Exists(filePath), "File with no hash must not be deleted");
    }

    // ── Rollback: overwritten files ────────────────────────────────────────

    [Fact]
    public void Rollback_RestoresOverwrittenFile_FromBackup()
    {
        var destPath = Path.Combine(TempDir, "shared.dll");
        var backupPath = Path.Combine(TempDir, "backup", "shared.dll.bak");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        File.WriteAllText(backupPath, "original dll");
        File.WriteAllText(destPath, "mod-installed dll");

        var transaction = new InstallTransaction
        {
            Id = Guid.NewGuid(),
            ModId = Guid.NewGuid(),
            ModName = "Test Mod",
            State = TransactionState.Committed,
            FilesOverwritten =
            [
                new TransactionFileRecord
                {
                    DestinationPath = destPath,
                    BackupPath = backupPath,
                }
            ],
        };
        TransactionService.Instance.Add(transaction);

        var result = TransactionService.Instance.Rollback(transaction.Id);

        Assert.True(result.IsComplete);
        Assert.Empty(result.FailedFiles);
        Assert.Equal("original dll", File.ReadAllText(destPath));
    }

    // ── Rollback: partial failure ──────────────────────────────────────────

    [Fact]
    public void Rollback_MarksFailed_WhenBackupFileMissing()
    {
        var destPath = Path.Combine(TempDir, "shared.dll");
        File.WriteAllText(destPath, "mod-installed dll");

        var transaction = new InstallTransaction
        {
            Id = Guid.NewGuid(),
            ModId = Guid.NewGuid(),
            ModName = "Test Mod",
            State = TransactionState.Committed,
            FilesOverwritten =
            [
                new TransactionFileRecord
                {
                    DestinationPath = destPath,
                    BackupPath = Path.Combine(TempDir, "nonexistent.bak"), // missing
                }
            ],
        };
        TransactionService.Instance.Add(transaction);

        var result = TransactionService.Instance.Rollback(transaction.Id);

        Assert.False(result.IsComplete);
        Assert.True(result.IsFailed);
        Assert.Contains(destPath, result.FailedFiles);
        Assert.Equal(TransactionState.PartialRollback,
            TransactionService.Instance.Transactions.First(t => t.Id == transaction.Id).State);
    }

    // ── Persistence ────────────────────────────────────────────────────────

    [Fact]
    public void Load_EmptyTransactionList_WhenFileDoesNotExist()
    {
        // AppDataDir is isolated and empty; transactions.json does not exist
        TransactionService.Instance.Reset();

        Assert.Empty(TransactionService.Instance.Transactions);
    }

    [Fact]
    public void Rollback_ReturnsUnavailable_WhenAlreadyRolledBack()
    {
        var filePath = Path.Combine(TempDir, "plugin.dll");
        File.WriteAllText(filePath, "content");
        var hash = ComputeHash(filePath);

        var transaction = CommittedTransaction(filePath, installedHash: hash);
        TransactionService.Instance.Add(transaction);

        // First rollback succeeds
        var first = TransactionService.Instance.Rollback(transaction.Id);
        Assert.True(first.IsComplete);

        // Second rollback must be blocked
        var second = TransactionService.Instance.Rollback(transaction.Id);
        Assert.True(second.IsUnavailable);
        Assert.Contains("already in state", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Domain model backward-compatibility ────────────────────────────────

    [Fact]
    public void InstalledMod_NullTransactionId_DeserializesWithoutError()
    {
        // Simulate a library.json record written before TransactionId was added
        const string json = """
            {
              "Id": "11111111-1111-1111-1111-111111111111",
              "Name": "Old Mod",
              "Type": 0,
              "TypeLabel": "LSPDFR Plugin",
              "TypeColor": "#6B7280",
              "Version": "1.0",
              "Author": "Tester",
              "SourcePath": "",
              "InstallPath": "",
              "DlcPackName": "",
              "IsEnabled": true,
              "InstalledFiles": [],
              "InstalledAt": "2024-01-01T00:00:00",
              "LoadOrderPriority": 0,
              "HasConflict": false,
              "IsInstalling": false,
              "DetectionScore": 100,
              "Notes": ""
            }
            """;

        var mod = JsonSerializer.Deserialize<InstalledMod>(json);

        Assert.NotNull(mod);
        Assert.Null(mod.TransactionId);
        Assert.Equal("Old Mod", mod.Name);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), mod.Id);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static InstallTransaction CommittedTransaction(string filePath, string? installedHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            ModId = Guid.NewGuid(),
            ModName = "Test Mod",
            State = TransactionState.Committed,
            FilesAdded =
            [
                new TransactionFileRecord
                {
                    DestinationPath = filePath,
                    InstalledHash = installedHash,
                }
            ],
        };

    private static string ComputeHash(string path)
    {
        using var fs = File.OpenRead(path);
        var bytes = System.Security.Cryptography.SHA256.HashData(fs);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
