namespace LSPDFRManager.LocalApi.Dtos;

public record AppConfigDto(
    string GtaPath,
    string BackupPath,
    bool AutoBackupOnInstall,
    bool ConfirmBeforeUninstall,
    bool AutoLaunchAfterInstall,
    bool AutoInstallHighConfidence,
    bool DeleteTempAfterInstall,
    int MaxInstallLogEntries,
    int MinimumFreeDiskSpaceMb,
    bool AutoStartBrowseApi,
    string? BrowseApiPath,
    string BrowseApiBaseUrl,
    bool AutoBackupEnabled,
    string BackupScheduleMode,
    int MaxBackupCount,
    bool CompressBackups,
    bool ShowSetupWizardOnStartup,
    bool CheckForUpdatesOnStartup,
    double UiScale
);

public record UpdateConfigRequest(
    string? GtaPath,
    string? BackupPath,
    bool? AutoBackupOnInstall,
    bool? ConfirmBeforeUninstall,
    bool? AutoLaunchAfterInstall,
    bool? AutoInstallHighConfidence,
    bool? DeleteTempAfterInstall,
    int? MaxInstallLogEntries,
    int? MinimumFreeDiskSpaceMb,
    bool? AutoStartBrowseApi,
    string? BrowseApiPath,
    string? BrowseApiBaseUrl,
    bool? AutoBackupEnabled,
    string? BackupScheduleMode,
    int? MaxBackupCount,
    bool? CompressBackups,
    bool? ShowSetupWizardOnStartup,
    bool? CheckForUpdatesOnStartup,
    double? UiScale
);

public record ValidateGtaPathRequest(string Path);

public record ValidateGtaPathResponse(bool Valid, string? Error);
