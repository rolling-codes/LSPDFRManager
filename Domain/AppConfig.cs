using LSPDFRManager.Services;

namespace LSPDFRManager.Domain;

public class AppConfig
{
    private static readonly JsonFileStore<AppConfig> Store = new(AppDataPaths.ConfigFile);
    private static AppConfig? _instance;

    public static AppConfig Instance => _instance ??= Store.LoadOrDefault(static () => new AppConfig());

    public string GtaPath { get; set; } =
        @"C:\Program Files\Rockstar Games\Grand Theft Auto V";

    public string BackupPath { get; set; } = Path.Combine(AppDataPaths.Root, "Backups");

    public bool AutoBackupOnInstall { get; set; } = true;
    public bool ConfirmBeforeUninstall { get; set; } = true;
    public bool AutoLaunchAfterInstall { get; set; }
    public DateTime? LastBackupDate { get; set; }

    /// <summary>
    /// When true, mods with detection confidence ≥ 75 % are queued for install
    /// immediately after drop/download without requiring the user to click Install.
    /// </summary>
    public bool AutoInstallHighConfidence { get; set; }

    /// <summary>
    /// When true, the source archive is deleted from the temp download folder
    /// after a successful install.
    /// </summary>
    public bool DeleteTempAfterInstall { get; set; } = true;

    // Browse API
    public bool AutoStartBrowseApi { get; set; }
    public string? BrowseApiPath { get; set; }
    public string BrowseApiBaseUrl { get; set; } = "http://localhost:5284";

    // Backup Scheduler
    public bool AutoBackupEnabled { get; set; }
    public BackupScheduleMode BackupScheduleMode { get; set; } = BackupScheduleMode.ManualOnly;
    public int MaxBackupCount { get; set; } = 10;
    public bool CompressBackups { get; set; } = true;

    // Profiles
    public string? ActiveProfileId { get; set; }

    // Setup Wizard
    public bool ShowSetupWizardOnStartup { get; set; } = true;

    // Diagnostics
    public DateTime? LastDiagnosticsScanUtc { get; set; }

    // Update Checker
    public bool CheckForUpdatesOnStartup { get; set; }
    public DateTime? LastUpdateCheckUtc { get; set; }

    // Game Version
    public string? LastKnownGameVersion { get; set; }
    public DateTime? LastKnownGameVersionDate { get; set; }

    public void Save() => Store.Save(this);
}
