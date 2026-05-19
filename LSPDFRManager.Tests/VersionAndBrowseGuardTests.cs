using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

public class VersionAndBrowseGuardTests
{
    [Fact]
    public void AssemblyVersion_Is_3_7_18_0()
    {
        var version = typeof(MainViewModel).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal(new Version(3, 7, 18, 0), version);
    }

    [Fact]
    public void BrowseViewModel_IsBrowserReady_DefaultsFalse()
    {
        var vm = new BrowseViewModel();
        Assert.False(vm.IsBrowserReady);
        Assert.False(vm.CanTriggerInstall);
    }

    [Fact]
    public void BrowseViewModel_CanTriggerInstall_TrueOnlyWhenBrowserReady()
    {
        var vm = new BrowseViewModel();
        vm.IsBrowserReady = true;
        Assert.True(vm.CanTriggerInstall);
        vm.IsBrowserReady = false;
        Assert.False(vm.CanTriggerInstall);
    }

    [Fact]
    public void BrowseViewModel_IsBrowserReady_RaisesPropertyChangedForCanTriggerInstall()
    {
        var vm = new BrowseViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.IsBrowserReady = true;

        Assert.Contains(nameof(BrowseViewModel.IsBrowserReady), changed);
        Assert.Contains(nameof(BrowseViewModel.CanTriggerInstall), changed);
    }
}
