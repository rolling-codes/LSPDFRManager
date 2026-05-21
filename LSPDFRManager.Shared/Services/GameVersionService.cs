using System.Diagnostics;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class GameVersionService
{
    public GameVersionInfo GetCurrentVersion()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var exe = Path.Combine(gtaPath, "GTA5.exe");

        string? version = null;
        if (File.Exists(exe))
        {
            try { version = FileVersionInfo.GetVersionInfo(exe).FileVersion; } catch { }
        }

        var previous = AppConfig.Instance.LastKnownGameVersion;
        var changed = version is not null && previous is not null && !version.Equals(previous, StringComparison.OrdinalIgnoreCase);

        if (version is not null && version != previous)
        {
            AppConfig.Instance.LastKnownGameVersion = version;
            AppConfig.Instance.LastKnownGameVersionDate = DateTime.UtcNow;
            AppConfig.Instance.Save();
        }

        return new GameVersionInfo
        {
            Version = version,
            DetectedAt = DateTime.UtcNow,
            ChangedSinceLastCheck = changed,
            PreviousVersion = previous,
        };
    }
}
