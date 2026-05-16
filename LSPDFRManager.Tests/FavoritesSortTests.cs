using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for the "Favorites first" sort logic mirrored from
/// <see cref="LSPDFRManager.ViewModels.LibraryViewModel.ApplyFilters"/>.
/// The LINQ expression is tested directly to avoid WPF dispatcher dependencies.
/// </summary>
public class FavoritesSortTests
{
    [Fact]
    public void FavoritesFirst_FavoriteModLeadsList_NonFavoritesSortedNewestFirst()
    {
        // A — not a favorite, newest
        var modA = new InstalledMod { Name = "A", IsFavorite = false, InstalledAt = new DateTime(2024, 3, 1) };
        // B — favorite, oldest
        var modB = new InstalledMod { Name = "B", IsFavorite = true,  InstalledAt = new DateTime(2024, 1, 1) };
        // C — not a favorite, oldest of the non-favorites
        var modC = new InstalledMod { Name = "C", IsFavorite = false, InstalledAt = new DateTime(2024, 2, 1) };

        var mods = new List<InstalledMod> { modA, modB, modC };

        var sorted = mods
            .OrderByDescending(m => m.IsFavorite)
            .ThenByDescending(m => m.InstalledAt)
            .ToList();

        // B is the only favorite → must come first
        Assert.Equal("B", sorted[0].Name);
        // Among non-favorites, newest (A) beats older (C)
        Assert.Equal("A", sorted[1].Name);
        Assert.Equal("C", sorted[2].Name);
    }

    [Fact]
    public void FavoritesFirst_MultipleFavorites_SortedNewestFirst()
    {
        var older  = new InstalledMod { Name = "OlderFav",  IsFavorite = true, InstalledAt = new DateTime(2024, 1, 1) };
        var newer  = new InstalledMod { Name = "NewerFav",  IsFavorite = true, InstalledAt = new DateTime(2024, 6, 1) };
        var normal = new InstalledMod { Name = "Normal",    IsFavorite = false, InstalledAt = new DateTime(2025, 1, 1) };

        var mods = new List<InstalledMod> { older, newer, normal };

        var sorted = mods
            .OrderByDescending(m => m.IsFavorite)
            .ThenByDescending(m => m.InstalledAt)
            .ToList();

        // Both favorites come before the non-favorite, and among favorites newer wins
        Assert.Equal("NewerFav", sorted[0].Name);
        Assert.Equal("OlderFav", sorted[1].Name);
        Assert.Equal("Normal",   sorted[2].Name);
    }

    [Fact]
    public void FavoritesFirst_NoFavorites_SortsNewestFirst()
    {
        var older  = new InstalledMod { Name = "Older",  IsFavorite = false, InstalledAt = new DateTime(2024, 1, 1) };
        var newer  = new InstalledMod { Name = "Newer",  IsFavorite = false, InstalledAt = new DateTime(2024, 6, 1) };

        var mods = new List<InstalledMod> { older, newer };

        var sorted = mods
            .OrderByDescending(m => m.IsFavorite)
            .ThenByDescending(m => m.InstalledAt)
            .ToList();

        Assert.Equal("Newer", sorted[0].Name);
        Assert.Equal("Older", sorted[1].Name);
    }
}
