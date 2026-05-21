namespace LSPDFRManager.Domain;

public class VersionBundle
{
    public string? GtaVersion { get; init; }
    public string? LspdfrVersion { get; init; }
    public string? RagePluginHookVersion { get; init; }
    public string? ScriptHookVVersion { get; init; }
    public string? ScriptHookVDotNetVersion { get; init; }

    public string? GtaHash { get; init; }
    public string? LspdfrHash { get; init; }
    public string? RagePluginHookHash { get; init; }
    public string? ScriptHookVHash { get; init; }
    public string? ScriptHookVDotNetHash { get; init; }

    public bool GtaPresent { get; init; }
    public bool LspdfrPresent { get; init; }
    public bool RagePluginHookPresent { get; init; }

    public long? GtaExeFileSizeBytes { get; init; }
    public DateTime? GtaExeLastWriteTimeUtc { get; init; }

    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
