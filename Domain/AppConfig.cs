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

    public void Save() => Store.Save(this);
}
