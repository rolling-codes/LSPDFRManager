namespace LSPDFRManager.Domain;

public enum ChangeHistoryAction
{
    Installed,
    Uninstalled,
    Enabled,
    Disabled,
    ProfileApplied,
    SafeLaunchApplied,
    BackupCreated,
    RestorePerformed,
    ScanPerformed,
    CrashLogAnalyzed,
    DependencyIgnored,
    SettingsChanged,
    ApiStarted,
    ApiStopped,
    OperationFailed,
    RestorePointCreated,
    RestorePointRestored,
}
