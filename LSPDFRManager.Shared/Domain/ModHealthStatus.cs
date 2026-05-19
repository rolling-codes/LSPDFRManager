namespace LSPDFRManager.Domain;

public enum ModHealthStatus
{
    /// <summary>No problems detected.</summary>
    Healthy,
    /// <summary>Minor issues detected — mod works but has warnings.</summary>
    NeedsAttention,
    /// <summary>Critical issues that likely prevent the mod from functioning.</summary>
    Broken,
    /// <summary>No scan data available for this mod.</summary>
    Unknown,
}

/// <summary>Per-mod health roll-up produced by <c>ModHealthScoringService</c>.</summary>
/// <param name="ModId">ID of the affected mod.</param>
/// <param name="Status">Overall health verdict.</param>
/// <param name="Issues">Human-readable issue summaries contributing to the verdict.</param>
public sealed record ModHealthResult(
    Guid ModId,
    ModHealthStatus Status,
    IReadOnlyList<string> Issues);
