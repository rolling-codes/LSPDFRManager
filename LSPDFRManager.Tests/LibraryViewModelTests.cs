using LSPDFRManager.Models;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

public class LibraryViewModelTests
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public LibraryViewModelTests()
    {
        _library.Mods.Clear();
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

    [Fact]
    public void SelectedStatusFilter_DisabledOnly_ShowsOnlyDisabledMods()
    {
        _library.Mods.Add(new InstalledMod { Name = "Enabled", TypeLabel = "LSPDFR Plugin", IsEnabled = true });
        _library.Mods.Add(new InstalledMod { Name = "Disabled", TypeLabel = "LSPDFR Plugin", IsEnabled = false });

        var vm = new LibraryViewModel
        {
            SelectedStatusFilter = "Disabled only",
        };

        Assert.Single(vm.FilteredMods);
        Assert.Equal("Disabled", vm.FilteredMods[0].Name);
    }
}
