using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface IOivSourceScanner
{
    /// <summary>
    /// Walks <paramref name="sourcePaths"/> (files or folders) and populates the Files
    /// collection of a new plan derived from <paramref name="template"/>.
    /// Blocked or missing paths are recorded as warnings; the template's existing
    /// Errors/Warnings are preserved.
    /// </summary>
    OivPackagePlan Scan(IReadOnlyList<string> sourcePaths, OivPackagePlan template);
}
