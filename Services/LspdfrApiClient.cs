using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using LSPDFRManager.Core;

namespace LSPDFRManager.Services;

// ── DTOs ────────────────────────────────────────────────────────────────────

/// <summary>A single mod result returned from the lcpdfr.com search API.</summary>
public record ModSearchResult(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("author")]      string Author,
    [property: JsonPropertyName("version")]     string Version,
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("url")]         string Url,
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("imageUrl")]    string ImageUrl,
    [property: JsonPropertyName("updatedAt")]   string UpdatedAt
);

// ── Client ──────────────────────────────────────────────────────────────────

/// <summary>
/// HTTP client for the LSPDFR Manager API backend, which provides clean JSON
/// access to lcpdfr.com mod data without the caller needing to scrape HTML.
/// The API runs locally (started with the app) or can be pointed at a hosted
/// instance via <see cref="BaseUrl"/>.
/// </summary>
public class LspdfrApiClient
{
    private static LspdfrApiClient? _instance;
    public static LspdfrApiClient Instance => _instance ??= new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Base URL of the API server (default: local instance on port 5284).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5284";

    // ── Public methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Searches lcpdfr.com for mods matching <paramref name="query"/>.
    /// Returns an empty list if the API is unavailable.
    /// </summary>
    public async Task<List<ModSearchResult>> SearchAsync(string query,
        string? category = null, CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/api/mods/search?q={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrWhiteSpace(category))
                url += $"&category={Uri.EscapeDataString(category)}";

            var json    = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            var results = JsonSerializer.Deserialize<List<ModSearchResult>>(json, _json);
            return results ?? [];
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Browse API unavailable: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Returns the latest version information for a specific mod by its lcpdfr.com ID.
    /// Returns <c>null</c> if the API is unavailable or the mod is not found.
    /// </summary>
    public async Task<ModSearchResult?> GetModAsync(string modId,
        CancellationToken ct = default)
    {
        try
        {
            var json   = await _http.GetStringAsync($"{BaseUrl}/api/mods/{modId}", ct)
                                    .ConfigureAwait(false);
            return JsonSerializer.Deserialize<ModSearchResult>(json, _json);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Browse API — mod lookup failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the file at <paramref name="downloadUrl"/> to a temporary path
    /// and returns that path, ready to pass to the install pipeline.
    /// </summary>
    public async Task<string?> DownloadToTempAsync(string downloadUrl, string fileName,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var tempDir  = Path.Combine(Path.GetTempPath(), "LSPDFRManager_downloads");
            Directory.CreateDirectory(tempDir);
            var destPath = Path.Combine(tempDir, fileName);

            using var response = await _http.GetAsync(downloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total   = response.Content.Headers.ContentLength ?? -1;
            long got    = 0;

            await using var src  = await response.Content.ReadAsStreamAsync(ct);
            await using var dest = File.Create(destPath);

            var buf = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, read), ct);
                got += read;
                if (total > 0) progress?.Report((int)(got * 100 / total));
            }

            return destPath;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Download failed: {downloadUrl}", ex);
            return null;
        }
    }

    /// <summary>
    /// Checks whether the local API server is reachable.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/health",
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
