using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for PatrolReadinessResult, PatrolReadinessService, and PatrolReadinessViewModel.
/// </summary>
public class PatrolReadinessTests : CommandCenterTestBase
{
    public PatrolReadinessTests()
    {
        TransactionService.Instance.Reset();
    }

    // ── ComputeState ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeState_Blocking_ReturnsNotReady()
    {
        var state = PatrolReadinessResult.ComputeState(
            blocking: ["missing exe"],
            warnings: [],
            passing: []);

        Assert.Equal(PatrolReadinessState.NotReady, state);
    }

    [Fact]
    public void ComputeState_WarningsOnly_ReturnsWarning()
    {
        var state = PatrolReadinessResult.ComputeState(
            blocking: [],
            warnings: ["some warning"],
            passing: ["check passed"]);

        Assert.Equal(PatrolReadinessState.Warning, state);
    }

    [Fact]
    public void ComputeState_PassingOnly_ReturnsReady()
    {
        var state = PatrolReadinessResult.ComputeState(
            blocking: [],
            warnings: [],
            passing: ["check passed"]);

        Assert.Equal(PatrolReadinessState.Ready, state);
    }

    [Fact]
    public void ComputeState_Nothing_ReturnsUnknown()
    {
        var state = PatrolReadinessResult.ComputeState(
            blocking: [],
            warnings: [],
            passing: []);

        Assert.Equal(PatrolReadinessState.Unknown, state);
    }

    // ── Service: GTA path checks ──────────────────────────────────────────

    [Fact]
    public async Task Service_BlankGtaPath_ReturnsNotReadyWithMessage()
    {
        AppConfig.Instance.GtaPath = "";

        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        Assert.Equal(PatrolReadinessState.NotReady, result.OverallState);
        Assert.Contains(result.BlockingIssues, b => b.Contains("GTA V path is not configured"));
    }

    [Fact]
    public async Task Service_NonexistentDirectory_ReturnsNotReady()
    {
        AppConfig.Instance.GtaPath = Path.Combine(TempDir, "nonexistent_dir");

        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        Assert.Equal(PatrolReadinessState.NotReady, result.OverallState);
        Assert.Contains(result.BlockingIssues, b => b.Contains("does not exist"));
    }

    // ── Service: file presence checks ─────────────────────────────────────

    [Fact]
    public async Task Service_AllRequiredFilesPresent_ReturnsReadyOrWarning()
    {
        // Place all required files
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "fake");
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "fake");

        AppConfig.Instance.GtaPath = GtaDir;
        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        // No blocking issues — must be Ready or Warning (ScriptHookV absent → Warning)
        Assert.Empty(result.BlockingIssues);
        Assert.True(
            result.OverallState == PatrolReadinessState.Ready ||
            result.OverallState == PatrolReadinessState.Warning);
    }

    [Fact]
    public async Task Service_MissingRph_ReturnsNotReady()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "fake");
        // RAGEPluginHook.exe intentionally absent

        AppConfig.Instance.GtaPath = GtaDir;
        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        Assert.Equal(PatrolReadinessState.NotReady, result.OverallState);
        Assert.Contains(result.BlockingIssues, b => b.Contains("RAGEPluginHook"));
    }

    [Fact]
    public async Task Service_MissingScriptHookV_ReturnsWarningNotNotReady()
    {
        // All required files present; ScriptHookV absent → warning only
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "fake");
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "fake");
        // ScriptHookV.dll intentionally absent

        AppConfig.Instance.GtaPath = GtaDir;
        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        Assert.Empty(result.BlockingIssues);
        Assert.NotEqual(PatrolReadinessState.NotReady, result.OverallState);
        Assert.Contains(result.Warnings, w => w.Contains("ScriptHookV"));
    }

    // ── Service: PartialRollback transaction ──────────────────────────────

    [Fact]
    public async Task Service_PartialRollbackTransaction_AddsWarning()
    {
        // Create a PartialRollback transaction in the service
        var filePath = Path.Combine(TempDir, "plugin.dll");
        File.WriteAllText(filePath, "content");

        var transaction = new InstallTransaction
        {
            Id = Guid.NewGuid(),
            ModId = Guid.NewGuid(),
            ModName = "BadMod",
            State = TransactionState.PartialRollback,
            FilesAdded = [],
            FilesOverwritten = [],
        };
        TransactionService.Instance.Add(transaction);

        // All required files present to avoid blocking issues
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "fake");
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "fake");

        AppConfig.Instance.GtaPath = GtaDir;
        var svc = new PatrolReadinessService();
        var result = await svc.CheckAsync();

        Assert.Contains(result.Warnings, w => w.Contains("BadMod") && w.Contains("partial"));
    }

    // ── ViewModel: StatusText mapping ────────────────────────────────────

    [Theory]
    [InlineData(PatrolReadinessState.Ready,    "READY TO PATROL")]
    [InlineData(PatrolReadinessState.Warning,  "WARNINGS")]
    [InlineData(PatrolReadinessState.NotReady, "NOT READY")]
    [InlineData(PatrolReadinessState.Unknown,  "UNKNOWN")]
    public void ViewModel_StatusText_MapsAllStates(PatrolReadinessState state, string expected)
    {
        var vm = new PatrolReadinessViewModel();

        // Inject a result via reflection to avoid async complexity
        var resultProp = typeof(PatrolReadinessViewModel)
            .GetProperty("Result",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public)!;

        var result = new PatrolReadinessResult
        {
            OverallState   = state,
            BlockingIssues = state == PatrolReadinessState.NotReady ? ["blocking"] : [],
            Warnings       = state == PatrolReadinessState.Warning  ? ["warning"]  : [],
            PassingChecks  = state == PatrolReadinessState.Ready    ? ["passing"]  : [],
        };
        resultProp.SetValue(vm, result);

        Assert.Equal(expected, vm.StatusText);
    }

    // ── ViewModel: HasBlockingIssues / HasWarnings / HasPassingChecks ─────

    [Fact]
    public void ViewModel_HasFlags_CorrectWhenResultSet()
    {
        var vm = new PatrolReadinessViewModel();

        // Before any result
        Assert.False(vm.HasBlockingIssues);
        Assert.False(vm.HasWarnings);
        Assert.False(vm.HasPassingChecks);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task ViewModel_CheckAsync_SetsHasResult()
    {
        // Blank path → NotReady, but HasResult should be true after check
        AppConfig.Instance.GtaPath = "";

        var vm = new PatrolReadinessViewModel();
        await vm.CheckAsync();

        Assert.True(vm.HasResult);
        Assert.True(vm.HasBlockingIssues);
        Assert.False(vm.IsChecking);
    }

    [Fact]
    public void ViewModel_HasBlockingIssues_IsTrueWhenResultHasBlockingIssues()
    {
        var vm = new PatrolReadinessViewModel();
        var resultProp = typeof(PatrolReadinessViewModel)
            .GetProperty("Result",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public)!;

        var result = new PatrolReadinessResult
        {
            OverallState   = PatrolReadinessState.NotReady,
            BlockingIssues = ["RAGEPluginHook.exe not found"],
            Warnings       = [],
            PassingChecks  = [],
        };
        resultProp.SetValue(vm, result);

        Assert.True(vm.HasBlockingIssues);
        Assert.False(vm.HasWarnings);
    }

    [Fact]
    public void ViewModel_HasWarnings_IsTrueWhenResultHasWarnings()
    {
        var vm = new PatrolReadinessViewModel();
        var resultProp = typeof(PatrolReadinessViewModel)
            .GetProperty("Result",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public)!;

        var result = new PatrolReadinessResult
        {
            OverallState   = PatrolReadinessState.Warning,
            BlockingIssues = [],
            Warnings       = ["ScriptHookV.dll is missing"],
            PassingChecks  = ["GTA5.exe found"],
        };
        resultProp.SetValue(vm, result);

        Assert.True(vm.HasWarnings);
        Assert.False(vm.HasBlockingIssues);
    }
}
