using System.Net.Http.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class BrowseEndpoints
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static void MapBrowse(this WebApplication app)
    {
        app.MapGet("/api/v1/browse/search", async (string? q, int page = 1) =>
        {
            var query = q ?? string.Empty;

            var browseApiBase = AppConfig.Instance.BrowseApiBaseUrl;
            if (string.IsNullOrWhiteSpace(browseApiBase))
                browseApiBase = "http://localhost:5284";

            try
            {
                var url = $"{browseApiBase.TrimEnd('/')}/api/mods/search?q={Uri.EscapeDataString(query)}";
                var remoteResults = await Http
                    .GetFromJsonAsync<List<BrowseApiModSummary>>(url)
                    .ConfigureAwait(false);

                if (remoteResults is null)
                    return Results.Ok(new BrowseSearchResponse([], page, 0, false));

                const int pageSize = 20;
                var paged   = remoteResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                var hasMore = remoteResults.Count > page * pageSize;

                var dtos = paged.Select(m => new BrowseModDto(
                    Id:          m.Id,
                    Name:        m.Title,
                    Author:      string.IsNullOrEmpty(m.Author)      ? null : m.Author,
                    Description: string.IsNullOrEmpty(m.Description) ? null : m.Description,
                    Version:     string.IsNullOrEmpty(m.Version)     ? null : m.Version,
                    ImageUrl:    string.IsNullOrEmpty(m.ImageUrl)    ? null : m.ImageUrl,
                    DownloadUrl: string.IsNullOrEmpty(m.DownloadUrl) ? null : m.DownloadUrl,
                    PageUrl:     string.IsNullOrEmpty(m.Url)         ? null : m.Url
                )).ToList();

                return Results.Ok(new BrowseSearchResponse(dtos, page, remoteResults.Count, hasMore));
            }
            catch (HttpRequestException)
            {
                // Browse API not running — return empty success so the UI can show a friendly message
                return Results.Ok(new BrowseSearchResponse([], page, 0, false));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Browse proxy error: {ex.Message}");
            }
        });
    }

    // Local projection of the Browse API's ModSummary shape
    private record BrowseApiModSummary(
        string Id,
        string Title,
        string Author,
        string Version,
        string Type,
        string Description,
        string Url,
        string DownloadUrl,
        string ImageUrl,
        string UpdatedAt
    );
}
