using System.IO;
using System.Text.Json;

namespace LSPDFRManager.Models;

public class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "config.json");

    private static AppConfig? _instance;
    public static AppConfig Instance => _instance ??= Load();

    public string GtaPath { get; set; } =
        @"C:\Program Files\Rockstar Games\Grand Theft Auto V";

    public string BackupPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "Backups");

    public bool AutoBackupOnInstall { get; set; } = true;
    public bool ConfirmBeforeUninstall { get; set; } = true;
    public DateTime? LastBackupDate { get; set; }

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
