namespace LSPDFRManager.Domain;

/// <summary>Lifecycle stage of a feature — controls default flag state and UI visibility.</summary>
public enum FeatureStage
{
    /// <summary>Stable, enabled by default.</summary>
    Stable,
    /// <summary>Preview: enabled by default but may have rough edges.</summary>
    Preview,
    /// <summary>Experimental: disabled by default.</summary>
    Experimental,
    /// <summary>Developer-only: hidden from non-dev UI.</summary>
    DevOnly,
}

/// <summary>Static description of one app feature, used by <see cref="IFeatureFlagService"/>.</summary>
/// <param name="Id">Stable, lowercase, kebab-case identifier (e.g. "patrol-readiness").</param>
/// <param name="Title">Short display name.</param>
/// <param name="Description">One-sentence explanation for Settings UI.</param>
/// <param name="Stage">Controls default enabled state and visibility.</param>
public sealed record FeatureManifest(
    string Id,
    string Title,
    string Description,
    FeatureStage Stage);
