using System.Security.Cryptography;
using System.Text.Json;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class RollbackResult
{
    public bool IsComplete { get; init; }
    public bool IsPartial { get; init; }
    public bool IsFailed { get; init; }
    public bool IsUnavailable { get; init; }
    public List<string> SkippedFiles { get; init; } = [];
    public List<string> FailedFiles { get; init; } = [];
    public string? Error { get; init; }

    public static RollbackResult Unavailable(string reason) =>
        new() { IsUnavailable = true, Error = reason };
}

/// <summary>
/// Persists install transactions and performs user-initiated rollback.
/// Rollback removes added files (only if unchanged since install) and restores overwritten files from backup.
/// </summary>
public class TransactionService
{
    private static readonly Lazy<TransactionService> LazyInstance = new(static () => new TransactionService());
    public static TransactionService Instance => LazyInstance.Value;

    // Use AppDataPaths.Root so test overrides via AppDataPaths.OverrideRoot() are respected.
    private static string FilePath =>
        Path.Combine(AppDataPaths.Root, "transactions.json");

    /// <summary>Returns the persistent backup folder path for a given transaction ID (may not exist yet).</summary>
    public static string BackupFolderFor(Guid transactionId) =>
        Path.Combine(AppDataPaths.Root, "Transactions", transactionId.ToString("N"), "backups");

    private List<InstallTransaction> _transactions = [];

    private TransactionService() => Load();

    public IReadOnlyList<InstallTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>Returns the most recent committed transaction for the given mod, or null.</summary>
    public InstallTransaction? GetByModId(Guid modId) =>
        _transactions.LastOrDefault(t => t.ModId == modId && t.State == TransactionState.Committed);

    public void Add(InstallTransaction transaction)
    {
        _transactions.Add(transaction);
        Save();
    }

    /// <summary>Clears in-memory state and reloads from the current FilePath. Used by tests after path override.</summary>
    internal void Reset()
    {
        _transactions = [];
        Load();
    }

    /// <summary>
    /// Rolls back an install transaction:
    /// - Restores overwritten files from backup.
    /// - Deletes added files only if their content is unchanged from install time (hash matches).
    /// - Files with no recorded hash (too large to hash at install time) are skipped, not deleted.
    /// - Marks the transaction as RolledBack or PartialRollback.
    /// </summary>
    public RollbackResult Rollback(Guid transactionId)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == transactionId);
        if (transaction is null)
            return RollbackResult.Unavailable("Transaction record not found.");

        if (transaction.State != TransactionState.Committed)
            return RollbackResult.Unavailable($"Transaction is already in state '{transaction.State}'.");

        var skipped = new List<string>();
        var failed = new List<string>();

        // Restore overwritten files first so the user is never left with missing files
        foreach (var file in transaction.FilesOverwritten)
        {
            if (string.IsNullOrEmpty(file.BackupPath) || !File.Exists(file.BackupPath))
            {
                failed.Add(file.DestinationPath);
                AppLogger.Warning($"[ROLLBACK] Backup missing for overwritten file: {file.DestinationPath}");
                continue;
            }

            try
            {
                var dir = Path.GetDirectoryName(file.DestinationPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.Copy(file.BackupPath, file.DestinationPath, overwrite: true);
                AppLogger.Info($"[ROLLBACK] Restored: {file.DestinationPath}");
            }
            catch (Exception ex)
            {
                failed.Add(file.DestinationPath);
                AppLogger.Warning($"[ROLLBACK] Restore failed for '{file.DestinationPath}': {ex.Message}");
            }
        }

        // Remove files that were newly added by this install
        foreach (var file in transaction.FilesAdded)
        {
            if (!File.Exists(file.DestinationPath))
                continue; // already gone — fine

            // When InstalledHash is null the file was too large to hash at install time.
            // Treat unknown state as unsafe-to-delete: skip and warn rather than silently destroying data.
            if (string.IsNullOrEmpty(file.InstalledHash))
            {
                skipped.Add(file.DestinationPath);
                AppLogger.Warning($"[ROLLBACK] Skipped file (no hash recorded, cannot verify unchanged): {file.DestinationPath}");
                continue;
            }

            // Skip deletion if the file content has changed since install
            var currentHash = TryComputeHash(file.DestinationPath);
            if (currentHash is not null &&
                !currentHash.Equals(file.InstalledHash, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(file.DestinationPath);
                AppLogger.Warning($"[ROLLBACK] Skipped modified file (hash mismatch): {file.DestinationPath}");
                continue;
            }

            try
            {
                File.Delete(file.DestinationPath);
                AppLogger.Info($"[ROLLBACK] Removed: {file.DestinationPath}");
            }
            catch (Exception ex)
            {
                failed.Add(file.DestinationPath);
                AppLogger.Warning($"[ROLLBACK] Delete failed for '{file.DestinationPath}': {ex.Message}");
            }
        }

        // Reverse dlclist.xml entry if this mod added one
        if (transaction.WasDlcEntry)
        {
            if (!string.IsNullOrWhiteSpace(transaction.DlcPackName))
            {
                try
                {
                    DlcListService.RemoveEntry(transaction.DlcPackName);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[ROLLBACK] dlclist.xml removal failed: {ex.Message}");
                }
            }
            else
            {
                AppLogger.Warning("[ROLLBACK] WasDlcEntry is true but DlcPackName is empty — dlclist.xml entry cannot be reversed.");
            }
        }

        // Clean up the persistent backup folder
        if (!string.IsNullOrEmpty(transaction.BackupFolder) &&
            Directory.Exists(transaction.BackupFolder))
        {
            try
            {
                Directory.Delete(transaction.BackupFolder, recursive: true);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[ROLLBACK] Backup folder cleanup failed: {ex.Message}");
            }
        }

        bool isPartial = skipped.Count > 0 || failed.Count > 0;
        transaction.State = isPartial ? TransactionState.PartialRollback : TransactionState.RolledBack;
        Save();

        return new RollbackResult
        {
            IsComplete = !isPartial,
            IsPartial = isPartial && failed.Count == 0,
            IsFailed = failed.Count > 0,
            SkippedFiles = skipped,
            FailedFiles = failed,
        };
    }

    private static string? TryComputeHash(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 50 * 1024 * 1024) return null;
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            _transactions = JsonSerializer.Deserialize<List<InstallTransaction>>(json) ?? [];
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"TransactionService: failed to load transactions: {ex.Message}");
            _transactions = [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.Root);
            var json = JsonSerializer.Serialize(_transactions,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"TransactionService: failed to save transactions: {ex.Message}");
        }
    }
}
