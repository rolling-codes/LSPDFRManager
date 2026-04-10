using LSPDFRManager.Models;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="ModLibraryService"/> using fresh non-singleton instances
/// so each test starts with an empty, isolated library.
/// </summary>
public class ModLibraryServiceTests
{
    // Create a new instance (not the singleton) for each test.
    // The default constructor calls Load() which is a no-op when library.json
    // does not exist at the standard path, but to stay fully isolated we clear
    // the Mods collection immediately after construction.
    private static ModLibraryService Fresh()
    {
        var lib = new ModLibraryService();
        lib.Mods.Clear();
        return lib;
    }

    private static InstalledMod Mod(string name, string type = "Plugin",
        string dlcPack = "", string[] files = null!) => new()
    {
        Name = name,
        TypeLabel = type,
        DlcPackName = dlcPack,
        InstalledFiles = files?.ToList() ?? [],
    };

    // ── Add / Remove ──────────────────────────────────────────────────────

    [Fact]
    public void Add_IncreasesCount()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("ELS"));
        Assert.Single(lib.Mods);
    }

    [Fact]
    public void Remove_ByExistingId_RemovesMod()
    {
        var lib = Fresh();
        var mod = Mod("ELS");
        lib.Mods.Add(mod);
        lib.Remove(mod.Id);
        Assert.Empty(lib.Mods);
    }

    [Fact]
    public void Remove_ByUnknownId_DoesNotThrow()
    {
        var lib = Fresh();
        lib.Remove(Guid.NewGuid()); // no-op, must not throw
    }

    // ── Search ────────────────────────────────────────────────────────────

    [Fact]
    public void Search_EmptyQuery_ReturnsAll()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("ELS"));
        lib.Mods.Add(Mod("LSPDFR"));
        Assert.Equal(2, lib.Search("").Count());
    }

    [Fact]
    public void Search_ByName_ReturnsMatchingMod()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Emergency Lighting System"));
        lib.Mods.Add(Mod("Traffic Policer"));
        var results = lib.Search("lighting").ToList();
        Assert.Single(results);
        Assert.Equal("Emergency Lighting System", results[0].Name);
    }

    [Fact]
    public void Search_ByTypeLabel_ReturnsMatchingMods()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Pursuit Manager", type: "LSPDFR Plugin"));
        lib.Mods.Add(Mod("Realism Dispatch", type: "LSPDFR Plugin"));
        lib.Mods.Add(Mod("Ford Explorer", type: "Vehicle DLC"));
        var results = lib.Search("plugin").ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_CaseInsensitive_FindsMod()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Traffic Policer"));
        Assert.Single(lib.Search("TRAFFIC"));
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("ELS"));
        Assert.Empty(lib.Search("zzznomatch"));
    }

    // ── Enable / Disable ─────────────────────────────────────────────────

    [Fact]
    public void SetEnabled_False_SetsIsEnabledFalse()
    {
        var lib = Fresh();
        var mod = Mod("ELS");
        lib.Mods.Add(mod);
        lib.SetEnabled(mod.Id, false);
        Assert.False(lib.Mods[0].IsEnabled);
    }

    [Fact]
    public void SetEnabled_TrueAfterFalse_RestoresEnabled()
    {
        var lib = Fresh();
        var mod = Mod("ELS");
        mod.IsEnabled = false;
        lib.Mods.Add(mod);
        lib.SetEnabled(mod.Id, true);
        Assert.True(lib.Mods[0].IsEnabled);
    }

    // ── DLC conflict detection ─────────────────────────────────────────────

    [Fact]
    public void IsDlcPackInstalled_WhenPresent_ReturnsTrue()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Car Pack", dlcPack: "myaddon"));
        Assert.True(lib.IsDlcPackInstalled("myaddon"));
    }

    [Fact]
    public void IsDlcPackInstalled_CaseInsensitive()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Car Pack", dlcPack: "MyAddon"));
        Assert.True(lib.IsDlcPackInstalled("myaddon"));
    }

    [Fact]
    public void IsDlcPackInstalled_WhenAbsent_ReturnsFalse()
    {
        var lib = Fresh();
        Assert.False(lib.IsDlcPackInstalled("nonexistent"));
    }

    [Fact]
    public void FindConflicts_DuplicateDlcPack_ReturnsIssue()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Car A", dlcPack: "shared_pack"));

        var candidate = Mod("Car B", dlcPack: "shared_pack");
        lib.Mods.Add(candidate);

        var conflicts = lib.FindConflicts(candidate);
        Assert.Contains(conflicts, c => c.Contains("shared_pack"));
    }

    [Fact]
    public void FindConflicts_OverlappingFiles_ReturnsIssue()
    {
        var lib = Fresh();
        var existing = Mod("Mod A", files: [@"C:\GTA5\x64e.rpf"]);
        lib.Mods.Add(existing);

        var candidate = Mod("Mod B", files: [@"C:\GTA5\x64e.rpf"]);
        lib.Mods.Add(candidate);

        var conflicts = lib.FindConflicts(candidate);
        Assert.NotEmpty(conflicts);
    }

    [Fact]
    public void FindConflicts_NoOverlap_ReturnsEmpty()
    {
        var lib = Fresh();
        lib.Mods.Add(Mod("Mod A", files: [@"C:\GTA5\file_a.rpf"]));

        var candidate = Mod("Mod B", files: [@"C:\GTA5\file_b.rpf"]);
        lib.Mods.Add(candidate);

        Assert.Empty(lib.FindConflicts(candidate));
    }
}
