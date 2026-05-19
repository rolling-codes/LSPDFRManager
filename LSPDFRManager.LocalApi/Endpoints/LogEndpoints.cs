using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class LogEndpoints
{
    private static readonly Dictionary<string, Func<string>> LogPaths = new()
    {
        ["manager"]            = () => AppDataPaths.LogFile,
        ["browse-api"]         = () => AppDataPaths.BrowseApiLogFile,
        ["rph"]                = () => Path.Combine(AppConfig.Instance.GtaPath, "RagePluginHook.log"),
        ["scripthookv"]        = () => Path.Combine(AppConfig.Instance.GtaPath, "ScriptHookV.log"),
        ["scripthookvdotnet"]  = () => Path.Combine(AppConfig.Instance.GtaPath, "ScriptHookVDotNet.log"),
    };

    private static readonly Dictionary<string, string> LogLabels = new()
    {
        ["manager"]           = "Manager Log",
        ["browse-api"]        = "Browse API Log",
        ["rph"]               = "RagePluginHook.log",
        ["scripthookv"]       = "ScriptHookV.log",
        ["scripthookvdotnet"] = "ScriptHookVDotNet.log",
    };

    public static void MapLogs(this WebApplication app)
    {
        app.MapGet("/api/v1/logs", () =>
        {
            var available = LogPaths
                .Where(kv => File.Exists(kv.Value()))
                .Select(kv => new LogFileInfoDto(kv.Key, LogLabels[kv.Key]))
                .ToList();

            return Results.Ok(new LogsAvailableResponse(available));
        });

        app.MapGet("/api/v1/logs/{name}", (string name, int tail = 200) =>
        {
            tail = Math.Clamp(tail, 1, 1000);

            if (!LogPaths.TryGetValue(name, out var pathFn))
                return Results.NotFound($"Unknown log: {name}");

            var path = pathFn();
            if (!File.Exists(path))
                return Results.NotFound($"Log not found: {name}");

            try
            {
                var all   = File.ReadAllLines(path);
                var lines = all.Length <= tail
                    ? all
                    : all[^tail..];

                return Results.Ok(new LogLinesResponse(name, LogLabels[name], lines, all.Length));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read log '{name}': {ex.Message}");
            }
        });
    }
}
