using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface IOivPackageBuilder
{
    /// <summary>
    /// Assembles the OIV ZIP at <paramref name="outputPath"/> from <paramref name="plan"/>.
    /// Never mutates the GTA V install folder.
    /// </summary>
    Task<OivBuildResult> BuildAsync(OivPackagePlan plan, string outputPath);
}

public sealed record OivBuildResult(bool Success, string? Error = null, int FilesWritten = 0);
