using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Verifies that older library.json records missing newer fields deserialize
/// to the correct defaults, preserving backward compatibility.
/// </summary>
[Collection("AppData serial")]
public class BackwardCompatTests
{
    // Minimal JSON that represents a library.json record written before
    // IsFavorite, TotalSizeBytes, and TransactionId were introduced.
    private const string MinimalJson = """
        {
          "Id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          "Name": "Old Mod",
          "IsEnabled": true,
          "InstalledFiles": [],
          "InstalledAt": "2023-06-01T00:00:00"
        }
        """;

    [Fact]
    public void OlderRecord_MissingIsFavorite_DefaultsFalse()
    {
        var mod = JsonSerializer.Deserialize<InstalledMod>(MinimalJson);

        Assert.NotNull(mod);
        Assert.False(mod.IsFavorite);
    }

    [Fact]
    public void OlderRecord_MissingTotalSizeBytes_DefaultsZero()
    {
        var mod = JsonSerializer.Deserialize<InstalledMod>(MinimalJson);

        Assert.NotNull(mod);
        Assert.Equal(0L, mod.TotalSizeBytes);
    }

    [Fact]
    public void OlderRecord_MissingTransactionId_DefaultsNull()
    {
        var mod = JsonSerializer.Deserialize<InstalledMod>(MinimalJson);

        Assert.NotNull(mod);
        Assert.Null(mod.TransactionId);
    }
}
