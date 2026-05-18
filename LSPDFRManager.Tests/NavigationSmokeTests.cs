using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("CommandCenter")]
public class NavigationSmokeTests : CommandCenterTestBase
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void MainViewModel_Constructs_WithoutThrowing_WhenWizardSkipped()
    {
        AppConfig.Instance.GtaPath = GtaDir;
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var vm = new MainViewModel();

        Assert.NotNull(vm);
    }

    [Fact]
    public void MainViewModel_Constructs_WithoutThrowing_WhenWizardTriggered()
    {
        AppConfig.Instance.GtaPath = "";
        AppConfig.Instance.ShowSetupWizardOnStartup = false;

        var vm = new MainViewModel();

        Assert.IsType<SetupWizardViewModel>(vm.CurrentView);
    }

    // ── ViewModel properties non-null ─────────────────────────────────────────

    [Fact]
    public void AllViewModelProperties_AreNonNull_AfterConstruction()
    {
        AppConfig.Instance.GtaPath = GtaDir;
        AppConfig.Instance.ShowSetupWizardOnStartup = false;

        var vm = new MainViewModel();

        Assert.NotNull(vm.DashboardVM);
        Assert.NotNull(vm.LibraryVM);
        Assert.NotNull(vm.InstallVM);
        Assert.NotNull(vm.ConfigVM);
        Assert.NotNull(vm.BrowseVM);
        Assert.NotNull(vm.DiagnosticsVM);
        Assert.NotNull(vm.ProfilesVM);
        Assert.NotNull(vm.BackupsVM);
        Assert.NotNull(vm.HistoryVM);
        Assert.NotNull(vm.LogViewerVM);
        Assert.NotNull(vm.SettingsVM);
        Assert.NotNull(vm.OivVM);
        Assert.NotNull(vm.DevDiagnosticsVM);
        Assert.NotNull(vm.PatrolReadinessVM);
        Assert.NotNull(vm.SafeModeVM);
        Assert.NotNull(vm.CleanupVM);
        Assert.NotNull(vm.SetupWizardVM);
    }

    // ── Navigate() resolves to correct ViewModel type ─────────────────────────

    [Theory]
    [InlineData("Home",             typeof(DashboardViewModel))]
    [InlineData("Library",          typeof(LibraryViewModel))]
    [InlineData("Install",          typeof(InstallViewModel))]
    [InlineData("Browse",           typeof(BrowseViewModel))]
    [InlineData("Config",           typeof(ConfigViewModel))]
    [InlineData("ModConfig",        typeof(ConfigViewModel))]
    [InlineData("Diagnostics",      typeof(DiagnosticsViewModel))]
    [InlineData("Profiles",         typeof(ProfilesViewModel))]
    [InlineData("Backups",          typeof(BackupsViewModel))]
    [InlineData("History",          typeof(HistoryViewModel))]
    [InlineData("Logs",             typeof(LogViewerViewModel))]
    [InlineData("Settings",         typeof(SettingsViewModel))]
    [InlineData("Oiv",              typeof(OivViewModel))]
    [InlineData("DevDiagnostics",   typeof(DevDiagnosticsViewModel))]
    [InlineData("PatrolReadiness",  typeof(PatrolReadinessDashboardViewModel))]
    [InlineData("SafeMode",         typeof(SafeModeViewModel))]
    [InlineData("Cleanup",          typeof(CleanupViewModel))]
    [InlineData("SetupWizard",      typeof(SetupWizardViewModel))]
    [InlineData("UnknownRoute",     typeof(DashboardViewModel))]
    public void Navigate_SetsCurrentView_ToCorrectType(string route, Type expectedType)
    {
        AppConfig.Instance.GtaPath = GtaDir;
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        var vm = new MainViewModel();

        vm.NavigateCommand.Execute(route);

        Assert.IsType(expectedType, vm.CurrentView);
    }

    // ── IsXxxActive reflects active route ─────────────────────────────────────

    [Fact]
    public void Navigate_Home_SetsIsHomeActive()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("Library");
        vm.NavigateCommand.Execute("Home");

        Assert.True(vm.IsHomeActive);
        Assert.False(vm.IsLibraryActive);
    }

    [Fact]
    public void Navigate_Library_SetsIsLibraryActive()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("Library");

        Assert.True(vm.IsLibraryActive);
        Assert.False(vm.IsHomeActive);
    }

    [Fact]
    public void Navigate_Cleanup_SetsIsCleanupActive()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("Cleanup");

        Assert.True(vm.IsCleanupActive);
        Assert.False(vm.IsHomeActive);
    }

    [Fact]
    public void Navigate_SafeMode_SetsIsSafeModeActive()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("SafeMode");

        Assert.True(vm.IsSafeModeActive);
        Assert.False(vm.IsHomeActive);
    }

    [Fact]
    public void Navigate_PatrolReadiness_SetsIsPatrolReadinessActive()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("PatrolReadiness");

        Assert.True(vm.IsPatrolReadinessActive);
    }

    // ── Lifecycle wiring ──────────────────────────────────────────────────────

    [Fact]
    public void CleanupVM_OnCancelled_NavigatesHome()
    {
        var vm = CreateNormalVm();
        vm.NavigateCommand.Execute("Cleanup");
        Assert.True(vm.IsCleanupActive);

        vm.CleanupVM.OnCancelled?.Invoke();

        Assert.True(vm.IsHomeActive);
        Assert.IsType<DashboardViewModel>(vm.CurrentView);
    }

    [Fact]
    public void SetupWizardVM_OnFinished_NavigatesHome()
    {
        AppConfig.Instance.GtaPath = "";
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        var vm = new MainViewModel();
        Assert.IsType<SetupWizardViewModel>(vm.CurrentView);

        vm.SetupWizardVM.OnFinished?.Invoke();

        Assert.True(vm.IsHomeActive);
        Assert.IsType<DashboardViewModel>(vm.CurrentView);
    }

    [Fact]
    public void SetupWizardVM_OnFinished_IsWired_WhenWizardStarts()
    {
        AppConfig.Instance.GtaPath = "";
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        var vm = new MainViewModel();

        Assert.NotNull(vm.SetupWizardVM.OnFinished);
    }

    [Fact]
    public void CleanupVM_OnCancelled_IsWired_OnStartup()
    {
        var vm = CreateNormalVm();

        Assert.NotNull(vm.CleanupVM.OnCancelled);
    }

    // ── Rapid nav switching does not throw ────────────────────────────────────

    [Fact]
    public void RapidTabSwitching_DoesNotThrow()
    {
        var vm = CreateNormalVm();
        var routes = new[]
        {
            "Home", "Library", "Install", "Browse", "PatrolReadiness",
            "SafeMode", "Cleanup", "Diagnostics", "Profiles", "Backups",
            "History", "Logs", "Settings", "ModConfig", "Oiv",
            "DevDiagnostics", "Home",
        };

        var ex = Record.Exception(() =>
        {
            foreach (var r in routes)
                vm.NavigateCommand.Execute(r);
        });

        Assert.Null(ex);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private MainViewModel CreateNormalVm()
    {
        AppConfig.Instance.GtaPath = GtaDir;
        AppConfig.Instance.ShowSetupWizardOnStartup = false;
        return new MainViewModel();
    }
}
