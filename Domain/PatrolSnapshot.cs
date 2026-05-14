namespace LSPDFRManager.Domain;

/// <summary>Records a known-good patrol setup state for later comparison or restore.</summary>
public sealed class PatrolSnapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    public string? GtaVersion { get; init; }
    public string? RphVersion { get; init; }
    public string? LspdfrVersion { get; init; }
    public string? ScriptHookVVersion { get; init; }
    public string[] EnabledPlugins { get; init; } = [];
    public string[] DisabledPlugins { get; init; } = [];
    public string[] AsiFiles { get; init; } = [];
    public string[] ElsFiles { get; init; } = [];
    /// <summary>SHA256 hashes of key config files, keyed by relative path.</summary>
    public Dictionary<string, string> ConfigHashes { get; init; } = [];
    public string? Notes { get; init; }
}
