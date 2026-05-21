using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ChangeHistoryService
{
    private static ChangeHistoryService? _instance;
    public static ChangeHistoryService Instance => _instance ??= new();

    private List<ChangeHistoryEntry> _entries = [];

    public IReadOnlyList<ChangeHistoryEntry> Entries => _entries;

    public void Load()
    {
        var path = AppDataPaths.ChangeHistoryFile;
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            _entries = System.Text.Json.JsonSerializer.Deserialize<List<ChangeHistoryEntry>>(json) ?? [];
        }
        catch { _entries = []; }
    }

    public void Record(ChangeHistoryAction action, string description, string? affectedFile = null, string? detail = null)
    {
        _entries.Insert(0, new ChangeHistoryEntry
        {
            Action = action,
            Description = description,
            AffectedFile = affectedFile,
            Detail = detail,
        });

        if (_entries.Count > 1000) _entries = _entries.Take(1000).ToList();
        Save();
    }

    public List<ChangeHistoryEntry> Filter(ChangeHistoryAction? action = null, DateTime? since = null, string? search = null)
    {
        IEnumerable<ChangeHistoryEntry> q = _entries;
        if (action.HasValue) q = q.Where(e => e.Action == action.Value);
        if (since.HasValue) q = q.Where(e => e.OccurredAt >= since.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(e => e.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || (e.AffectedFile?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        return q.ToList();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    public async Task ExportAsync(string outputPath, bool asJson)
    {
        if (asJson)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json);
        }
        else
        {
            var lines = _entries.Select(e => $"[{e.OccurredAt:yyyy-MM-dd HH:mm:ss}] [{e.Action}] {e.Description}");
            await File.WriteAllLinesAsync(outputPath, lines);
        }
    }

    private void Save()
    {
        try
        {
            var path = AppDataPaths.ChangeHistoryFile;
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(_entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }
}
