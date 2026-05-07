namespace LSPDFRManager.Domain;

public enum BackupScheduleMode
{
    ManualOnly,
    EveryLaunch,
    Daily,
    Weekly,
    BeforeProfileSwitch,
    BeforeInstall,
    BeforeSafeLaunch,
}
