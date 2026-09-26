using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ChangeHistoryService
{
    private static readonly Lazy<ChangeHistoryService> _instance = new(() => new ChangeHistoryService());
    public static ChangeHistoryService Instance => _instance.Value;

    private readonly JsonFileStore<List<ChangeHistoryEntry>> _store = new(AppDataPaths.ChangeHistoryFile);
    private readonly object _lock = new();
    private List<ChangeHistoryEntry> _entries;

    internal ChangeHistoryService() => _entries = _store.LoadOrDefault(() => []);

    // Returns a snapshot: callers iterate a stable copy, never the live list.
    public IReadOnlyList<ChangeHistoryEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public void Load()
    {
        var loaded = _store.LoadOrDefault(() => []);
        lock (_lock) _entries = loaded;
    }

    public void Record(ChangeHistoryAction action, string description, string? affectedFile = null, string? detail = null)
    {
        List<ChangeHistoryEntry> snapshot;
        lock (_lock)
        {
            _entries.Insert(0, new ChangeHistoryEntry
            {
                Action = action,
                Description = description,
                AffectedFile = affectedFile,
                Detail = detail,
            });

            if (_entries.Count > 1000) _entries = _entries.Take(1000).ToList();
            snapshot = _entries.ToList();
        }
        _store.Save(snapshot);
    }

    public List<ChangeHistoryEntry> Filter(ChangeHistoryAction? action = null, DateTime? since = null, string? search = null)
    {
        IEnumerable<ChangeHistoryEntry> q;
        lock (_lock) q = _entries.ToList();
        if (action.HasValue) q = q.Where(e => e.Action == action.Value);
        if (since.HasValue) q = q.Where(e => e.OccurredAt >= since.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(e => e.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || (e.AffectedFile?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        return q.ToList();
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
        _store.Save([]);
    }

    public async Task ExportAsync(string outputPath, bool asJson)
    {
        List<ChangeHistoryEntry> entries;
        lock (_lock) entries = _entries.ToList();

        if (asJson)
        {
            var sanitized = entries.Select(e => new
            {
                e.Id, e.Action, e.Description, e.OccurredAt, e.Detail,
                AffectedFile = SanitizePath(e.AffectedFile),
            });
            var json = System.Text.Json.JsonSerializer.Serialize(sanitized,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }
        else
        {
            var lines = entries.Select(e =>
            {
                var line = $"[{e.OccurredAt:yyyy-MM-dd HH:mm:ss}] [{e.Action}] {e.Description}";
                var af   = SanitizePath(e.AffectedFile);
                return string.IsNullOrEmpty(af) ? line : $"{line} — {af}";
            });
            await File.WriteAllLinesAsync(outputPath, lines);
        }
    }

    private static string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData) && path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            return "%APPDATA%" + path[appData.Length..];
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "%USERPROFILE%" + path[home.Length..];
        return path;
    }
}
