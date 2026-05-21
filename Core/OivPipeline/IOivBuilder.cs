namespace LSPDFRManager.OivPipeline;

using LSPDFRManager.OivPipeline.Models;

public sealed record OivBuildInput(
    string PackageName,
    string Version,
    string Author,
    IReadOnlyList<InstallOperation> Operations,
    string OutputDirectory
);

public sealed record OivBuildResult(bool Success, string? OutputPath, string? Error);

// TODO: Real .oiv generation requires OpenIV to be installed. The OpenIV package format
// beyond the basic assembly.xml/content/ structure is not publicly documented. Specifically:
// - RPF file embedding internals are undocumented
// - OpenIV CLI parameters are not officially published
// - Implement by wrapping OpenIV.API.dll or the OpenIV CLI if available.
public interface IOivBuilder
{
    Task<OivBuildResult> BuildAsync(OivBuildInput input, CancellationToken ct = default);
}

// Stub that always refuses with a clear explanation. Replace with a real implementation.
public sealed class StubOivBuilder : IOivBuilder
{
    public Task<OivBuildResult> BuildAsync(OivBuildInput input, CancellationToken ct = default)
    {
        const string error =
            "OIV package generation is not implemented. " +
            "The OpenIV package format internals (RPF handling, CLI parameters, signing) " +
            "are not publicly documented. Implement IOivBuilder against your OpenIV installation.";
        return Task.FromResult(new OivBuildResult(Success: false, OutputPath: null, Error: error));
    }
}
