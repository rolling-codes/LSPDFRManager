using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>Reads and writes feature-flag overrides. Implementation is file-backed JSON.</summary>
public interface IFeatureFlagService
{
    IReadOnlyList<FeatureManifest> AllFeatures { get; }
    bool IsEnabled(string featureId);
    void SetEnabled(string featureId, bool enabled);
    void Reset(string featureId);
    void Save();
}
