namespace LSPDFRManager.Domain;

public enum TransactionState { Committed, RolledBack, PartialRollback }

/// <summary>
/// One file entry recorded by an install transaction — either added or overwritten.
/// </summary>
public class TransactionFileRecord
{
    public string DestinationPath { get; set; } = "";

    /// <summary>Absolute path to the pre-install backup of this file. Null for newly-added files.</summary>
    public string? BackupPath { get; set; }

    /// <summary>SHA-256 hex string of the file as installed. Used to verify it is unchanged before rollback-delete.</summary>
    public string? InstalledHash { get; set; }
}

/// <summary>
/// Persisted record of one mod installation. Supports user-initiated rollback.
/// </summary>
public class InstallTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModId { get; set; }
    public string ModName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Files that did not exist before install.</summary>
    public List<TransactionFileRecord> FilesAdded { get; set; } = [];

    /// <summary>Files that existed before install; originals are preserved in <see cref="BackupFolder"/>.</summary>
    public List<TransactionFileRecord> FilesOverwritten { get; set; } = [];

    public TransactionState State { get; set; } = TransactionState.Committed;

    /// <summary>Absolute path to the folder containing rollback backups for this transaction.</summary>
    public string? BackupFolder { get; set; }

    /// <summary>Whether this mod added a dlclist.xml entry that rollback should reverse.</summary>
    public bool WasDlcEntry { get; set; }
    public string? DlcPackName { get; set; }
}
