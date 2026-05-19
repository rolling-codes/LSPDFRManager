namespace LSPDFRManager.Services;

/// <summary>
/// Prevents path traversal attacks during file extraction.
/// </summary>
public static class PathSafety
{
    /// <summary>
    /// Safely combines root and relative paths, ensuring the result stays within root.
    /// Throws if path traversal is detected.
    /// </summary>
    public static string GetSafePath(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(relativePath);

        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException($"Path traversal detected: {relativePath}");

        var sanitized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, sanitized));

        // Normalize root for comparison: ensure trailing separator
        var normalizedRoot = fullRoot + Path.DirectorySeparatorChar;

        // Check if combined path is root itself (empty relative path case)
        if (combined == fullRoot || combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return combined;

        throw new InvalidOperationException($"Path traversal detected: {relativePath}");
    }
}
