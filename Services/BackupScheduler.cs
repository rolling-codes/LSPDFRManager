using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class BackupScheduler
{
    private static BackupScheduler? _instance;
    public static BackupScheduler Instance => _instance ??= new();

    private readonly BackupService _backup = new();
    private List<BackupManifest> _manifests = [];
    public IReadOnlyList<BackupManifest> Manifests => _manifests;

    public void LoadManifests()
    {
        var path = AppDataPaths.BackupManifestFile;
        if (!File.Exists(path)) { _manifests = []; return; }
        try
        {
            var json = File.ReadAllText(path);
            _manifests = System.Text.Json.JsonSerializer.Deserialize<List<BackupManifest>>(json) ?? [];
        }
        catch { _manifests = []; }
    }

    public async Task RunIfDueAsync(BackupScheduleMode mode, IProgress<string>? progress = null)
    {
        if (!ShouldRunNow(mode)) return;
        await CreateBackupAsync(progress);
    }

    public async Task CreateBackupAsync(IProgress<string>? progress = null)
    {
        await _backup.CreateBackupAsync(progress);

        var manifest = new BackupManifest
        {
            FileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            BackupType = "Full",
            CreatedAt = DateTime.UtcNow,
            ModCount = ModLibraryService.Instance.Mods.Count,
        };

        _manifests.Insert(0, manifest);
        EnforceMaxBackups();
        SaveManifests();

        ChangeHistoryService.Instance.Record(ChangeHistoryAction.BackupCreated, "Backup created.");
    }

    private void EnforceMaxBackups()
    {
        var max = AppConfig.Instance.MaxBackupCount;
        if (_manifests.Count <= max) return;

        var toRemove = _manifests.Skip(max).ToList();
        foreach (var m in toRemove)
        {
            var path = Path.Combine(AppConfig.Instance.BackupPath, m.FileName);
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        _manifests = _manifests.Take(max).ToList();
    }

    private bool ShouldRunNow(BackupScheduleMode mode)
    {
        var last = AppConfig.Instance.LastBackupDate;
        return mode switch
        {
            BackupScheduleMode.EveryLaunch => true,
            BackupScheduleMode.Daily => last is null || (DateTime.UtcNow - last.Value).TotalHours >= 24,
            BackupScheduleMode.Weekly => last is null || (DateTime.UtcNow - last.Value).TotalDays >= 7,
            _ => false,
        };
    }

    private void SaveManifests()
    {
        var path = AppDataPaths.BackupManifestFile;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(_manifests, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
