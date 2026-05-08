using System.Net.Http;
using System.Text.Json;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class UpdateCheckService
{
    private const string CurrentVersion = "3.5.0";
    private const string ReleasesApiUrl = "https://api.github.com/repos/rolling-codes/LSPDFRManager/releases/latest";

    private readonly HttpClient _http = new();

    public UpdateCheckService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "LSPDFRManager");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ReleasesApiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var latest = root.GetProperty("tag_name").GetString()?.TrimStart('v');
            var htmlUrl = root.GetProperty("html_url").GetString();

            var updateAvailable = latest is not null &&
                Version.TryParse(latest, out var latestVer) &&
                Version.TryParse(CurrentVersion, out var currentVer) &&
                latestVer > currentVer;

            AppConfig.Instance.LastUpdateCheckUtc = DateTime.UtcNow;
            AppConfig.Instance.Save();

            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = latest,
                UpdateAvailable = updateAvailable,
                ReleaseNotesUrl = htmlUrl,
                DownloadUrl = htmlUrl,
            };
        }
        catch
        {
            return new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                IsOffline = true,
            };
        }
    }
}
