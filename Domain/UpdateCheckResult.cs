namespace LSPDFRManager.Domain;

public class UpdateCheckResult
{
    public string CurrentVersion { get; init; } = "3.5.0";
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? ReleaseNotesUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsOffline { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}
