namespace LSPDFRManager.Domain;

/// <summary>
/// Persistent application settings stored at
/// <c>%APPDATA%\LSPDFRManager\config.json</c>.
/// Access the singleton via <see cref="Instance"/>; persist changes with
/// <see cref="Save"/>.
/// </summary>
public class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "config.json");

    private static AppConfig? _instance;

    /// <summary>
    /// Lazily-loaded singleton.  Loads from disk on first access; returns a
    /// default instance if the config file does not exist yet.
    /// </summary>
    public static AppConfig Instance => _instance ??= Load();

    /// <summary>Full path to the GTA V installation directory.</summary>
    public string GtaPath { get; set; } =
        @"C:\Program Files\Rockstar Games\Grand Theft Auto V";

    /// <summary>Directory where backup ZIPs are written.</summary>
    public string BackupPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "Backups");

    /// <summary>When <c>true</c>, a backup is created automatically before each new mod install.</summary>
    public bool AutoBackupOnInstall { get; set; } = true;

    /// <summary>When <c>true</c>, the user is prompted to confirm before uninstalling a mod.</summary>
    public bool ConfirmBeforeUninstall { get; set; } = true;
    public bool AutoLaunchAfterInstall { get; set; }

    /// <summary>UTC timestamp of the most recent backup, or <c>null</c> if none exists.</summary>
    public DateTime? LastBackupDate { get; set; }

    /// <summary>Serialises the current settings to disk.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig Load()
    {
        if (!File.Exists(ConfigPath)) return new AppConfig();
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }
}
