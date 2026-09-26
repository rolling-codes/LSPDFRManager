using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class RestorePointService
{
    private static readonly Lazy<RestorePointService> _instance = new(() => new RestorePointService());
    public static RestorePointService Instance => _instance.Value;

    private readonly object _lock = new();
    private List<RestorePoint> _points = [];

    // Snapshot: callers iterate a stable copy, never the live list.
    public IReadOnlyList<RestorePoint> Points
    {
        get { lock (_lock) return _points.ToList(); }
    }

    public RestorePointService() => Load();

    public void Load()
    {
        var indexPath = AppDataPaths.RestorePointsIndex;
        if (!File.Exists(indexPath)) return;
        try
        {
            var json = File.ReadAllText(indexPath);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<List<RestorePoint>>(json) ?? [];
            lock (_lock) _points = loaded;
        }
        catch { lock (_lock) _points = []; }
    }

    public virtual async Task SaveAsync(RestorePoint point, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RestorePoint> snapshot;
        lock (_lock)
        {
            _points.Insert(0, point);
            if (_points.Count > 50) _points = _points.Take(50).ToList();
            snapshot = _points.ToList();
        }
        await PersistIndexAsync(snapshot, cancellationToken);
        ChangeHistoryService.Instance.Record(ChangeHistoryAction.RestorePointCreated, $"Restore point created: {point.OperationName}", detail: point.Id);
    }

    public async Task RestoreAsync(RestorePoint point, IProgress<string>? progress = null)
    {
        var gtaPath = AppConfig.Instance.GtaPath;

        foreach (var entry in point.Entries)
        {
            try
            {
                string fullPath;
                try { fullPath = PathSafety.GetSafePath(gtaPath, entry.RelativePath); }
                catch
                {
                    progress?.Report($"Skipped unsafe path: {entry.RelativePath}");
                    AppLogger.Warning($"[RestorePoint] Rejected traversal path: {entry.RelativePath}");
                    continue;
                }
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
        List<RestorePoint> snapshot;
        lock (_lock)
        {
            _points.Remove(point);
            snapshot = _points.ToList();
        }
        await PersistIndexAsync(snapshot);
    }

    private static async Task PersistIndexAsync(List<RestorePoint> points, CancellationToken cancellationToken = default)
    {
        var dir = AppDataPaths.RestorePointsDirectory;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(points, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(AppDataPaths.RestorePointsIndex, json, cancellationToken);
    }
}
