using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class HistoryEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void MapHistory(this WebApplication app)
    {
        app.MapGet("/api/v1/history", (int limit = 50, int offset = 0) =>
        {
            limit  = Math.Clamp(limit,  1, 500);
            offset = Math.Max(offset, 0);

            var path = AppDataPaths.ChangeHistoryFile;

            if (!File.Exists(path))
                return Results.Ok(new HistoryResponse([], 0));

            try
            {
                var json    = File.ReadAllText(path);
                var entries = JsonSerializer.Deserialize<List<ChangeHistoryEntry>>(json, JsonOpts)
                              ?? [];

                var total   = entries.Count;
                var page    = entries.Skip(offset).Take(limit).Select(ToDto).ToList();

                return Results.Ok(new HistoryResponse(page, total));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read change history: {ex.Message}");
            }
        });
    }

    private static ChangeHistoryEntryDto ToDto(ChangeHistoryEntry e) =>
        new(e.Id, e.Action.ToString(), e.Description, SanitizePath(e.AffectedFile), e.Detail, e.OccurredAt);

    private static string? SanitizePath(string? path)
    {
        if (path is null) return null;
        var gta = AppConfig.Instance.GtaPath;
        if (!string.IsNullOrWhiteSpace(gta) && path.StartsWith(gta, StringComparison.OrdinalIgnoreCase))
            return "GTA:" + path[gta.Length..].TrimStart('\\', '/');
        var appData = AppDataPaths.Root;
        if (!string.IsNullOrWhiteSpace(appData) && path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            return "AppData:" + path[appData.Length..].TrimStart('\\', '/');
        return Path.GetFileName(path);
    }
}
