using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// File-backed feature flag service.  Overrides are stored in
/// <c>%APPDATA%\LSPDFRManager\feature-flags.json</c>.
/// Unknown IDs are silently ignored.  Missing override → stage default applies.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private static readonly Lazy<FeatureFlagService> LazyInstance =
        new(static () => new FeatureFlagService());

    public static FeatureFlagService Instance => LazyInstance.Value;

    private string FilePath => Path.Combine(AppDataPaths.Root, "feature-flags.json");

    // null = "use stage default", true/false = explicit override
    private Dictionary<string, bool> _overrides = [];

    public IReadOnlyList<FeatureManifest> AllFeatures => FeatureRegistry.All;

    private FeatureFlagService() => Load();

    public bool IsEnabled(string featureId)
    {
        if (_overrides.TryGetValue(featureId, out var overrideValue))
            return overrideValue;

        var manifest = FeatureRegistry.All.FirstOrDefault(f => f.Id == featureId);
        if (manifest is null) return false;

        return manifest.Stage switch
        {
            FeatureStage.Stable    => true,
            FeatureStage.Preview   => true,
            FeatureStage.Experimental => false,
            FeatureStage.DevOnly   => false,
            _ => false,
        };
    }

    public void SetEnabled(string featureId, bool enabled)
    {
        _overrides[featureId] = enabled;
        Save();
    }

    public void Reset(string featureId)
    {
        _overrides.Remove(featureId);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                System.Text.Json.JsonSerializer.Serialize(
                    _overrides,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"[FeatureFlags] Could not save: {ex.Message}");
        }
    }

    private void Load()
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            var json = File.ReadAllText(FilePath);
            _overrides = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, bool>>(json) ?? [];
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"[FeatureFlags] Could not load: {ex.Message}");
        }
    }

    // Test seam
    internal static void ResetInstance() => LazyInstance.Value._overrides.Clear();
}
