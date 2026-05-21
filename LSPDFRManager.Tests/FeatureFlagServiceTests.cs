using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class FeatureFlagServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public FeatureFlagServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ffs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(_tempRoot);
        FeatureFlagService.ResetInstance();
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void StableFeature_IsEnabledByDefault()
    {
        var svc = FeatureFlagService.Instance;
        Assert.True(svc.IsEnabled("patrol-readiness"));
    }

    [Fact]
    public void ExperimentalFeature_IsDisabledByDefault()
    {
        var svc = FeatureFlagService.Instance;
        Assert.False(svc.IsEnabled("mod-health-score"));
    }

    [Fact]
    public void SetEnabled_OverridesDefault()
    {
        var svc = FeatureFlagService.Instance;
        svc.SetEnabled("mod-health-score", true);
        Assert.True(svc.IsEnabled("mod-health-score"));
    }

    [Fact]
    public void Reset_RestoresDefault()
    {
        var svc = FeatureFlagService.Instance;
        svc.SetEnabled("patrol-readiness", false);
        svc.Reset("patrol-readiness");
        Assert.True(svc.IsEnabled("patrol-readiness")); // back to stable default
    }

    [Fact]
    public void UnknownFeature_ReturnsFalse()
    {
        var svc = FeatureFlagService.Instance;
        Assert.False(svc.IsEnabled("does-not-exist"));
    }

    [Fact]
    public void AllFeatures_ContainsRegisteredFeatures()
    {
        var svc = FeatureFlagService.Instance;
        Assert.NotEmpty(svc.AllFeatures);
        Assert.Contains(svc.AllFeatures, f => f.Id == "patrol-readiness");
    }

    [Fact]
    public void Save_PersistsOverrideFile()
    {
        var svc = FeatureFlagService.Instance;
        svc.SetEnabled("quarantine-folder", true);

        var flagFile = Path.Combine(_tempRoot, "feature-flags.json");
        Assert.True(File.Exists(flagFile));
        var content = File.ReadAllText(flagFile);
        Assert.Contains("quarantine-folder", content);
    }
}
