namespace LSPDFRManager.Services;

/// <summary>
/// Verifies that an absolute path resolves to a location inside a root
/// directory. Uses a separator-terminated root so a sibling such as
/// <c>C:\GTAV_evil</c> does not match root <c>C:\GTAV</c> by string prefix.
/// Use this to contain operations that consume paths from persisted state
/// (e.g. a safe-mode manifest) which a user or corruption could tamper with.
/// </summary>
public static class PathContainment
{
    public static bool IsWithin(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
            return false;

        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        string fullCandidate;
        try { fullCandidate = Path.GetFullPath(candidate); }
        catch { return false; }

        return fullCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
