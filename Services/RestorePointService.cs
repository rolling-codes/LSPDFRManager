using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class RestorePointService
{
    private static RestorePointService? _instance;
    public static RestorePointService Instance => _instance ??= new();

    private List<RestorePoint> _points = [];
    public IReadOnlyList<RestorePoint> Points => _points;

    public void Load()
    {
        var indexPath = AppDataPaths.RestorePointsIndex;
        if (!File.Exists(indexPath)) return;
        try
        {
            var json = File.ReadAllText(indexPath);
            _points = System.Text.Json.JsonSerializer.Deserialize<List<RestorePoint>>(json) ?? [];
        }
        catch { _points = []; }
    }

    public async Task SaveAsync(RestorePoint point)
    {
        _points.Insert(0, point);
        if (_points.Count > 50) _points = _points.Take(50).ToList();
        await PersistIndexAsync();
        ChangeHistoryService.Instance.Record(ChangeHistoryAction.RestorePointCreated, $"Restore point created: {point.OperationName}", detail: point.Id);
    }

    public async Task RestoreAsync(RestorePoint point, IProgress<string>? progress = null)
    {
        var gtaPath = AppConfig.Instance.GtaPath;

        foreach (var entry in point.Entries)
        {
            try
            {
                var fullPath = Path.Combine(gtaPath, entry.RelativePath);
                var disabledPath = fullPath.EndsWith(".disabled") ? fullPath : fullPath + ".disabled";
                var enabledPath = fullPath.EndsWith(".disabled") ? fullPath[..^".disabled".Length] : fullPath;

                if (entry.WasEnabled)
                {
                    if (File.Exists(disabledPath))
                        File.Move(disabledPath, enabledPath);
                }
                else
                {
                    if (File.Exists(enabledPath))
                        File.Move(enabledPath, disabledPath);
                }

                progress?.Report($"Restored: {entry.RelativePath}");
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {entry.RelativePath} — {ex.Message}");
            }
        }

        ChangeHistoryService.Instance.Record(ChangeHistoryAction.RestorePointRestored, $"Restore point restored: {point.OperationName}");
    }

    public async Task DeleteAsync(RestorePoint point)
    {
        _points.Remove(point);
        await PersistIndexAsync();
    }

    private async Task PersistIndexAsync()
    {
        var dir = AppDataPaths.RestorePointsDirectory;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(_points, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(AppDataPaths.RestorePointsIndex, json);
    }
}
