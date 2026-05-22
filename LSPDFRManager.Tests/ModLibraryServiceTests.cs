using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="ModLibraryService"/> using fresh non-singleton instances
/// so each test starts with an empty, isolated library.
/// </summary>
[Collection("AppData serial")]
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

    [Fact]
    public void Uninstall_WhenFileDeleteFails_KeepsLibraryRecord()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_uninstall_{Guid.NewGuid():N}");
        var filePath = Path.Combine(tempRoot, "plugins", "locked.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "locked");

        var savedGtaPath = AppConfig.Instance.GtaPath;
        AppConfig.Instance.GtaPath = tempRoot;

        try
        {
            var lib = Fresh();
            var mod = Mod("Locked Mod", files: [filePath]);
            mod.InstallPath = tempRoot;
            lib.Mods.Add(mod);

            using var locked = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var result = lib.Uninstall(mod.Id);

            Assert.False(result.Success);
            Assert.Contains(filePath, result.FailedFiles);
            Assert.Contains(mod, lib.Mods);
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            AppConfig.Instance.GtaPath = savedGtaPath ?? string.Empty;
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
        }
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

    // ── SyncWithDirectory ─────────────────────────────────────────────────

    [Fact]
    public void SyncWithDirectory_RemovesOrphanedMod_ReturnsCount()
    {
        var lib = Fresh();
        // InstalledFiles must be non-empty and point to a path that does not exist
        lib.Mods.Add(Mod("Ghost", files: [@"C:\nonexistent_lspm_xyz\ghost.dll"]));

        var pruned = lib.SyncWithDirectory();

        Assert.Equal(1, pruned);
        Assert.Empty(lib.Mods);
    }

    [Fact]
    public void SyncWithDirectory_LeavesModWithExistingFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var lib = Fresh();
            lib.Mods.Add(Mod("Live", files: [tempFile]));

            var pruned = lib.SyncWithDirectory();

            Assert.Equal(0, pruned);
            Assert.Single(lib.Mods);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void SyncWithDirectory_LeavesModWithDisabledVariant()
    {
        var tempFile = Path.GetTempFileName() + ".disabled";
        try
        {
            File.WriteAllText(tempFile, "");
            var lib = Fresh();
            // Record the path without .disabled — IsOrphaned checks both f and f+".disabled"
            lib.Mods.Add(Mod("Disabled", files: [tempFile[..^".disabled".Length]]));

            var pruned = lib.SyncWithDirectory();

            Assert.Equal(0, pruned);
        }
        finally { try { File.Delete(tempFile); } catch { } }
    }

    [Fact]
    public void SyncWithDirectory_IgnoresModWithEmptyFileList()
    {
        // Mods with no tracked files are not considered orphaned (pre-tracking installs)
        var lib = Fresh();
        lib.Mods.Add(Mod("OldMod"));   // files: []

        var pruned = lib.SyncWithDirectory();

        Assert.Equal(0, pruned);
        Assert.Single(lib.Mods);
    }

    [Fact]
    public void SyncWithDirectory_RemovesOnlyOrphanedMods_LeavesLiveModsIntact()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var lib = Fresh();
            lib.Mods.Add(Mod("Ghost", files: [@"C:\nonexistent_lspm_xyz\ghost.dll"]));
            lib.Mods.Add(Mod("Live",  files: [tempFile]));

            var pruned = lib.SyncWithDirectory();

            Assert.Equal(1, pruned);
            Assert.Single(lib.Mods);
            Assert.Equal("Live", lib.Mods[0].Name);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task SyncWithDirectory_CalledFromBackgroundThread_DoesNotDeadlock()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var lib = Fresh();
            lib.Mods.Add(Mod("Ghost", files: [@"C:\nonexistent_lspm_xyz\ghost.dll"]));
            lib.Mods.Add(Mod("Live",  files: [tempFile]));

            // Simulate the async path used by LibraryViewModel.SyncAndRefreshAsync
            var pruned = await Task.Run(lib.SyncWithDirectory);

            Assert.Equal(1, pruned);
            Assert.Equal("Live", lib.Mods[0].Name);
        }
        finally { File.Delete(tempFile); }
    }
}
