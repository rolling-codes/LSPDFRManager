using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class LogViewerService
{
    public record LogFile(string Label, string Path);

    public List<LogFile> GetAvailableLogs()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var logs = new List<LogFile>();

        AddIfExists(logs, "Manager Log", AppDataPaths.LogFile);
        AddIfExists(logs, "Browse API Log", AppDataPaths.BrowseApiLogFile);
        AddIfExists(logs, "RagePluginHook.log", Path.Combine(gtaPath, "RagePluginHook.log"));
        AddIfExists(logs, "ScriptHookV.log", Path.Combine(gtaPath, "ScriptHookV.log"));
        AddIfExists(logs, "ScriptHookVDotNet.log", Path.Combine(gtaPath, "ScriptHookVDotNet.log"));

        return logs;
    }

    public string[] ReadLines(string logPath)
    {
        if (!File.Exists(logPath)) return [];
        try { return File.ReadAllLines(logPath); }
        catch { return []; }
    }

    public string[] Search(string[] lines, string query, string? severityFilter = null)
    {
        IEnumerable<string> result = lines;
        if (!string.IsNullOrWhiteSpace(query))
            result = result.Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(severityFilter))
            result = result.Where(l => l.Contains(severityFilter, StringComparison.OrdinalIgnoreCase));
        return result.ToArray();
    }

    public async Task ExportAsync(string[] lines, string outputPath) =>
        await File.WriteAllLinesAsync(outputPath, lines);

    public void ClearManagerLog()
    {
        if (File.Exists(AppDataPaths.LogFile))
            File.WriteAllText(AppDataPaths.LogFile, "");
    }

    private static void AddIfExists(List<LogFile> list, string label, string path)
    {
        if (File.Exists(path)) list.Add(new LogFile(label, path));
    }
}
