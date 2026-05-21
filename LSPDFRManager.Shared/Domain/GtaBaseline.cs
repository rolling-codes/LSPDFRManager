namespace LSPDFRManager.Domain;

public class GtaBaseline
{
    public string? GtaVersion { get; init; }
    public string? GtaHash { get; init; }
    public long? GtaExeFileSizeBytes { get; init; }
    public DateTime? GtaExeLastWriteTimeUtc { get; init; }
    public DateTime CheckedAtUtc { get; init; }

    // Known-good snapshot fields (issue #38)
    public DateTime? MarkedKnownGoodAt { get; init; }
    public IReadOnlyList<string> EnabledPluginPaths { get; init; } = [];
    /// <summary>Relative path → SHA-256 hex of config file at snapshot time.</summary>
    public IReadOnlyDictionary<string, string> ConfigHashes { get; init; }
        = new Dictionary<string, string>();
    public string? RphVersion { get; init; }
}

/// <summary>Comparison result between current state and a known-good baseline.</summary>
public sealed record KnownGoodDiff(
    IReadOnlyList<string> AddedPlugins,
    IReadOnlyList<string> RemovedPlugins,
    IReadOnlyList<string> ChangedConfigs,
    bool HasChanges);

