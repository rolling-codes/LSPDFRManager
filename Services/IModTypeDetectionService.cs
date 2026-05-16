using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Pure, evidence-based mod-type classifier.
/// </summary>
/// <remarks>
/// Inputs are normalized archive entry paths (lowercase, forward-slash separated).
/// No file I/O is performed — callers list entries before calling.
/// </remarks>
public interface IModTypeDetectionService
{
    /// <summary>
    /// Classifies the archive described by <paramref name="entryPaths"/> and
    /// returns a result with primary type, confidence, secondary signals,
    /// evidence strings, and any warnings.
    /// </summary>
    /// <param name="entryPaths">
    /// Normalized (lowercase, forward-slash) relative paths of every file entry
    /// in the archive.  Directory entries may be included but are ignored.
    /// </param>
    /// <param name="archiveName">
    /// Optional archive filename (no path) used for keyword boosting.
    /// </param>
    ModTypeDetectionResult Detect(IReadOnlyList<string> entryPaths, string? archiveName = null);
}
