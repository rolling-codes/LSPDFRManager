namespace LSPDFRManager.Domain;

public class GameVersionInfo
{
    public string? Version { get; init; }
    public DateTime? DetectedAt { get; init; }
    public bool ChangedSinceLastCheck { get; init; }
    public string? PreviousVersion { get; init; }
}
