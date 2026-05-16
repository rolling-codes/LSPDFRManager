namespace LSPDFRManager.Domain;

/// <summary>
/// A single runtime dependency inferred from the detected mod type.
/// Severity stays at warning — the installer cannot prove the dependency is
/// absent without a live GTA installation check.
/// </summary>
public class DependencyWarning
{
    /// <summary>Display name of the required dependency (e.g. "LSPDFR").</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Why this dependency is needed, derived from the detected type
    /// (e.g. "Required by detected LSPDFR plugin files").
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>Mod type that produced this dependency requirement.</summary>
    public required ModType SourceType { get; init; }
}

/// <summary>
/// Output of <see cref="LSPDFRManager.Services.IDependencyDetectionService"/>.
/// Contains deduplicated dependency warnings for all detected types.
/// </summary>
public class DependencyDetectionResult
{
    public static readonly DependencyDetectionResult Empty =
        new() { Warnings = [] };

    public IReadOnlyList<DependencyWarning> Warnings { get; init; } = [];

    public bool HasWarnings => Warnings.Count > 0;
}
