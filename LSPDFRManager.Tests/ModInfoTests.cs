using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>Tests for <see cref="ModInfo"/> computed properties and defaults.</summary>
public class ModInfoTests
{
    // ── ConfidenceLabel ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.75f, "High")]
    [InlineData(1.00f, "High")]
    [InlineData(0.45f, "Medium")]
    [InlineData(0.74f, "Medium")]
    [InlineData(0.00f, "Low")]
    [InlineData(0.44f, "Low")]
    public void ConfidenceLabel_ReturnsExpectedBucket(float confidence, string expected)
    {
        var info = new ModInfo { Confidence = confidence };
        Assert.Equal(expected, info.ConfidenceLabel);
    }

    // ── Defaults ──────────────────────────────────────────────────────────

    [Fact]
    public void NewModInfo_HasEmptyCollections()
    {
        var info = new ModInfo();
        Assert.NotNull(info.Files);
        Assert.NotNull(info.Warnings);
        Assert.Empty(info.Files);
        Assert.Empty(info.Warnings);
    }

    [Fact]
    public void NewModInfo_DefaultTypeColor_IsGray()
    {
        var info = new ModInfo();
        Assert.Equal("#6B7280", info.TypeColor);
    }

    [Fact]
    public void NewModInfo_DefaultType_IsUnknown()
    {
        var info = new ModInfo();
        Assert.Equal(ModType.Unknown, info.Type);
    }
}

/// <summary>Tests for <see cref="InstalledMod"/> defaults.</summary>
public class InstalledModTests
{
    [Fact]
    public void NewInstalledMod_IsEnabledByDefault()
    {
        var mod = new InstalledMod();
        Assert.True(mod.IsEnabled);
    }

    [Fact]
    public void NewInstalledMod_HasUniqueId()
    {
        var a = new InstalledMod();
        var b = new InstalledMod();
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void NewInstalledMod_EmptyStringDefaults()
    {
        var mod = new InstalledMod();
        Assert.Equal("", mod.Name);
        Assert.Equal("", mod.Author);
        Assert.Equal("", mod.Version);
        Assert.Equal("", mod.DlcPackName);
    }

    [Fact]
    public void NewInstalledMod_HasEmptyFileList()
    {
        var mod = new InstalledMod();
        Assert.NotNull(mod.InstalledFiles);
        Assert.Empty(mod.InstalledFiles);
    }
}
