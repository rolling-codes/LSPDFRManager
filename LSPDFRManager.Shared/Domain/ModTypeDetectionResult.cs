namespace LSPDFRManager.Domain;

/// <summary>
/// A single type signal found in the archive, alongside the evidence that
/// produced it and a normalized confidence score in [0, 1].
/// </summary>
public record DetectedModType(
    ModType Type,
    float   Confidence,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Output of <c>IModTypeDetectionService.Detect</c>.  Pure value — no file I/O.
/// </summary>
public class ModTypeDetectionResult
{
    /// <summary>Strongest matched type, or <see cref="ModType.Unknown"/>.</summary>
    public ModType PrimaryType { get; init; } = ModType.Unknown;

    /// <summary>Normalized confidence for <see cref="PrimaryType"/> in [0, 1].</summary>
    public float Confidence { get; init; }

    /// <summary>Human-readable tier label derived from <see cref="Confidence"/>.</summary>
    public string ConfidenceLabel => Confidence >= 0.75f ? "High"
                                   : Confidence >= 0.45f ? "Medium"
                                   : "Low";

    /// <summary>
    /// Other type signals found above the secondary threshold, ordered by
    /// descending confidence.  Empty for unambiguous archives.
    /// </summary>
    public IReadOnlyList<DetectedModType> SecondaryTypes { get; init; } = [];

    /// <summary>
    /// Evidence strings that explain why <see cref="PrimaryType"/> was chosen
    /// (e.g. "Found .asi file: scripthookv.asi").
    /// </summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// Non-fatal warnings the installer or UI should surface
    /// (e.g. "Archive contains both ASI and config files — verify intended type").
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// <c>true</c> when two or more type signals are too close to call confidently
    /// (primary and first secondary confidence differ by less than 0.20).
    /// </summary>
    public bool IsMixed { get; init; }
}
