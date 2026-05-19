using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Pure mapper from mod-type detection result to dependency warnings.
/// No file I/O — operates entirely on the already-classified <see cref="ModTypeDetectionResult"/>.
/// </summary>
public interface IDependencyDetectionService
{
    /// <summary>
    /// Returns deduplicated dependency warnings for every type signal present
    /// in <paramref name="modTypeResult"/> (primary + secondaries).
    /// </summary>
    DependencyDetectionResult Detect(ModTypeDetectionResult modTypeResult);
}
