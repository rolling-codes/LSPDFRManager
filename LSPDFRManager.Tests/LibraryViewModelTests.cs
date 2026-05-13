using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("AppData serial")]
public class LibraryViewModelTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_lvm_{Guid.NewGuid():N}");
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public LibraryViewModelTests()
    {
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        _library.Mods.Clear();
    }

    public void Dispose()
    {
        try { _library.Mods.Clear(); } catch { }
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void SelectedSort_NameAscending_SortsAlphabetically()
    {
        _library.Mods.Add(new InstalledMod { Name = "Zulu", TypeLabel = "LSPDFR Plugin", InstalledAt = DateTime.UtcNow });
        _library.Mods.Add(new InstalledMod { Name = "Alpha", TypeLabel = "LSPDFR Plugin", InstalledAt = DateTime.UtcNow.AddMinutes(-2) });

        var vm = new LibraryViewModel
        {
            SelectedSort = "Name: A to Z",
        };

        Assert.Equal("Alpha", vm.FilteredMods[0].Name);
        Assert.Equal("Zulu", vm.FilteredMods[1].Name);
    }

    [Fact]
    public void SelectedSort_LoadOrder_UsesPersistentPriority()
    {
        _library.Mods.Add(new InstalledMod { Name = "Second", TypeLabel = "LSPDFR Plugin", LoadOrderPriority = 20, InstalledAt = DateTime.UtcNow });
        _library.Mods.Add(new InstalledMod { Name = "First", TypeLabel = "LSPDFR Plugin", LoadOrderPriority = 10, InstalledAt = DateTime.UtcNow.AddMinutes(-2) });

        var vm = new LibraryViewModel
        {
            SelectedSort = "Load order",
        };

        Assert.Equal("First", vm.FilteredMods[0].Name);
        Assert.Equal("Second", vm.FilteredMods[1].Name);
    }

    [Fact]
    public void DisableVisibleCommand_DisablesOnlyFilteredMods()
    {
        var plugin = new InstalledMod { Name = "A", TypeLabel = "LSPDFR Plugin", IsEnabled = true };
        var vehicle = new InstalledMod { Name = "B", TypeLabel = "Vehicle Add-On DLC", IsEnabled = true };
        _library.Mods.Add(plugin);
        _library.Mods.Add(vehicle);

        var vm = new LibraryViewModel
        {
            SelectedFilter = "LSPDFR Plugin",
        };

        vm.DisableVisibleCommand.Execute(null);

        Assert.False(plugin.IsEnabled);
        Assert.True(vehicle.IsEnabled);
    }
}
