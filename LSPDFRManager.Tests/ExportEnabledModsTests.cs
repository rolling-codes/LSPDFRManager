using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for the export-enabled-mods filtering logic extracted from
/// <see cref="LSPDFRManager.ViewModels.LibraryViewModel.ExportEnabledMods"/>.
/// The dialog and file-write are omitted; we exercise the LINQ predicate directly.
/// </summary>
public class ExportEnabledModsTests
{
    private static IEnumerable<InstalledMod> FilterEnabled(IEnumerable<InstalledMod> mods) =>
        mods.Where(mod => mod.IsEnabled)
            .OrderBy(mod => mod.LoadOrderPriority == 0 ? int.MaxValue : mod.LoadOrderPriority)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void FilterEnabled_TwoEnabledOneDisabled_ReturnsOnlyEnabledMods()
    {
        var mods = new List<InstalledMod>
        {
            new() { Name = "Mod A", IsEnabled = true  },
            new() { Name = "Mod B", IsEnabled = true  },
            new() { Name = "Mod C", IsEnabled = false },
        };

        var result = FilterEnabled(mods).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, mod => Assert.True(mod.IsEnabled));
        Assert.DoesNotContain(result, mod => mod.Name == "Mod C");
    }

    [Fact]
    public void FilterEnabled_AllDisabled_ReturnsEmpty()
    {
        var mods = new List<InstalledMod>
        {
            new() { Name = "Plugin X", IsEnabled = false },
            new() { Name = "Plugin Y", IsEnabled = false },
        };

        var result = FilterEnabled(mods).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void FilterEnabled_AllEnabled_ReturnsAll()
    {
        var mods = new List<InstalledMod>
        {
            new() { Name = "Alpha", IsEnabled = true },
            new() { Name = "Beta",  IsEnabled = true },
        };

        var result = FilterEnabled(mods).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterEnabled_RespectLoadOrderPriority_ZeroPriorityLast()
    {
        // LoadOrderPriority = 0 is treated as "unset" → sorted last
        var mods = new List<InstalledMod>
        {
            new() { Name = "No Priority",  IsEnabled = true, LoadOrderPriority = 0  },
            new() { Name = "High Priority", IsEnabled = true, LoadOrderPriority = 5  },
            new() { Name = "Low Priority",  IsEnabled = true, LoadOrderPriority = 10 },
        };

        var result = FilterEnabled(mods).ToList();

        Assert.Equal("High Priority", result[0].Name);
        Assert.Equal("Low Priority",  result[1].Name);
        Assert.Equal("No Priority",   result[2].Name);
    }
}
