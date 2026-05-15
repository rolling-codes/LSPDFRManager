namespace LSPDFRManager.Domain;

public enum InstallConflictAction
{
    KeepExisting,
    BackupAndReplace,
    RenameIncoming,
    Skip,
    CancelInstall,
}
