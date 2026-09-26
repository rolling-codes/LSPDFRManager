using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class PathContainmentTests
{
    [Fact]
    public void IsWithin_FileInsideRoot_ReturnsTrue()
    {
        Assert.True(PathContainment.IsWithin(@"C:\GTAV", @"C:\GTAV\plugins\lspdfr\x.dll"));
    }

    [Fact]
    public void IsWithin_RootItself_TrailingSeparatorTolerated()
    {
        Assert.True(PathContainment.IsWithin(@"C:\GTAV\", @"C:\GTAV\mods\y.dll"));
    }

    [Fact]
    public void IsWithin_SiblingPrefixDirectory_ReturnsFalse()
    {
        // C:\GTAV_evil must not match root C:\GTAV by string prefix.
        Assert.False(PathContainment.IsWithin(@"C:\GTAV", @"C:\GTAV_evil\payload.dll"));
    }

    [Fact]
    public void IsWithin_TraversalEscapingRoot_ReturnsFalse()
    {
        Assert.False(PathContainment.IsWithin(@"C:\GTAV", @"C:\GTAV\..\Windows\System32\evil.dll"));
    }

    [Fact]
    public void IsWithin_CompletelyDifferentRoot_ReturnsFalse()
    {
        Assert.False(PathContainment.IsWithin(@"C:\GTAV", @"D:\Other\thing.dll"));
    }

    [Theory]
    [InlineData("", @"C:\GTAV\x.dll")]
    [InlineData(@"C:\GTAV", "")]
    [InlineData(null, @"C:\GTAV\x.dll")]
    public void IsWithin_NullOrEmptyInput_ReturnsFalse(string? root, string? candidate)
    {
        Assert.False(PathContainment.IsWithin(root!, candidate!));
    }
}
