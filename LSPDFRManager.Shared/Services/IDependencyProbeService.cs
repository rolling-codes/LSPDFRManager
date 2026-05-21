using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface IDependencyProbeService
{
    DependencyProbeResult Probe(string gtaPath, DependencyDetectionResult dependencies);
}
