using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using LSPDFRManager.Core;

namespace LSPDFRManager.Services;

public record ModSearchResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("imageUrl")] string ImageUrl,
    [property: JsonPropertyName("updatedAt")] string UpdatedAt
);

public class LspdfrApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static LspdfrApiClient? _instance;
    public static LspdfrApiClient Instance => _instance ??= new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string BaseUrl { get; set; } = "http://localhost:5284";

    public async Task<List<ModSearchResult>> SearchAsync(
        string query,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var parameters = new List<string> { $"q={Uri.EscapeDataString(query)}" };
            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add($"category={Uri.EscapeDataString(category)}");
            }

            var url = $"{BaseUrl}/api/mods/search?{string.Join("&", parameters)}";
            var json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ModSearchResult>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Browse API search failed: {ex.Message}");
            return [];
        }
    }

    public async Task<ModSearchResult?> GetModAsync(string modId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return null;

        try
        {
            var json = await _http.GetStringAsync($"{BaseUrl}/api/mods/{modId}", cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<ModSearchResult>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Browse API mod lookup failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> DownloadToTempAsync(
        string downloadUrl,
        string fileName,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LSPDFRManager_downloads");
            Directory.CreateDirectory(tempDir);

            var safeFileName = string.IsNullOrWhiteSpace(fileName)
                ? $"{Guid.NewGuid():N}.zip"
                : string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));

            var destinationPath = Path.Combine(tempDir, safeFileName);

            using var response = await _http.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            long bytesRead = 0;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(destinationPath);

            var buffer = new byte[81920];
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report((int)(bytesRead * 100 / totalBytes));
            }

            progress?.Report(100);
            return destinationPath;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Download failed: {downloadUrl}", ex);
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(
                $"{BaseUrl}/health",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
