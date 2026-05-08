using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Integration tests for all Command Center (v3.5.0) features.
/// Each test uses real temp directories and fresh service instances — no mocks.
/// </summary>
public class CommandCenterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gtaDir;
    private readonly string _appDataDir;

    public CommandCenterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lsp_cc_{Guid.NewGuid():N}");
        _gtaDir  = Path.Combine(_tempDir, "GTA5");
        _appDataDir = Path.Combine(_tempDir, "AppData");

        Directory.CreateDirectory(_gtaDir);
        Directory.CreateDirectory(_appDataDir);

        AppDataPaths.OverrideRoot(_appDataDir);
        AppConfig.Instance.GtaPath    = _gtaDir;
        AppConfig.Instance.BackupPath = Path.Combine(_appDataDir, "Backups");
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Plugin Health Scanner ─────────────────────────────────────────

    [Fact]
    public void PluginScanner_NoIssues_WhenFolderEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_gtaDir, "plugins", "lspdfr"));
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Severity == PluginScanSeverity.Ok);
    }

    [Fact]
    public void PluginScanner_DetectsDuplicateDll()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "TestMod.dll"),          "data");
        File.WriteAllText(Path.Combine(dir, "TestMod.dll.disabled"), "data");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("Duplicate") && r.FileName.Contains("TestMod"));
    }

    [Fact]
    public void PluginScanner_DetectsZeroByteDll()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Empty.dll"), "");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("Zero-byte") && r.FileName == "Empty.dll");
    }

    [Fact]
    public void PluginScanner_DetectsZipInsidePluginsFolder()
    {
        var dir = Path.Combine(_gtaDir, "plugins");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "SomeMod.zip"), "PK...");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("archive found inside GTA V folder"));
    }

    [Fact]
    public void PluginScanner_DetectsDisabledFile()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Plugin.dll.disabled"), "data");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("disabled") && r.Severity == PluginScanSeverity.Info);
    }

    // ── Dependency Scanner ────────────────────────────────────────────

    [Fact]
    public void DependencyScanner_Gta5Exe_InstalledWhenPresent()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");

        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Installed, results.First(r => r.Name == "GTA5.exe").Status);
    }

    [Fact]
    public void DependencyScanner_Gta5Exe_MissingWhenAbsent()
    {
        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Missing, results.First(r => r.Name == "GTA5.exe").Status);
    }

    [Fact]
    public void DependencyScanner_DetectsDisabledDependency()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "ScriptHookV.dll.disabled"), "");

        var results = new DependencyScanner().Scan();

        Assert.Equal(DependencyStatus.Disabled, results.First(r => r.Name == "ScriptHookV.dll").Status);
    }

    // ── Crash Log Analyzer ────────────────────────────────────────────

    [Fact]
    public void CrashAnalyzer_DetectsFatalKeyword()
    {
        var logPath = Path.Combine(_tempDir, "RPH.log");
        File.WriteAllLines(logPath, ["[2024-01-01 12:00] FATAL: plugin crash detected"]);

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);

        Assert.Contains(findings, f => f.Severity == CrashLogSeverity.Fatal);
    }

    [Fact]
    public void CrashAnalyzer_DetectsCouldNotLoadKeyword()
    {
        var logPath = Path.Combine(_tempDir, "test.log");
        File.WriteAllLines(logPath, ["[INFO] Could not load assembly MyMod.dll"]);

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);

        Assert.Contains(findings, f => f.SuspectedCause.Contains("DLL failed to load"));
    }

    [Fact]
    public void CrashAnalyzer_ReturnsEmpty_ForCleanLog()
    {
        var logPath = Path.Combine(_tempDir, "clean.log");
        File.WriteAllLines(logPath, ["[INFO] Plugin loaded", "[INFO] All nominal"]);

        Assert.Empty(new CrashLogAnalyzer().AnalyzeFile(logPath));
    }

    [Fact]
    public async Task CrashAnalyzer_ExportsTxt()
    {
        var logPath = Path.Combine(_tempDir, "crash.log");
        File.WriteAllLines(logPath, ["FATAL: hard crash"]);
        var outPath = Path.Combine(_tempDir, "report.txt");

        var findings = new CrashLogAnalyzer().AnalyzeFile(logPath);
        await new CrashLogAnalyzer().ExportReportAsync(findings, outPath);

        Assert.True(File.Exists(outPath));
        Assert.NotEmpty(await File.ReadAllTextAsync(outPath));
    }

    // ── Profile Manager ───────────────────────────────────────────────

    [Fact]
    public void ProfileManager_Create_AddsProfile()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();
        var before = mgr.Profiles.Count;

        mgr.Create("My Profile");

        Assert.Equal(before + 1, mgr.Profiles.Count);
        Assert.Contains(mgr.Profiles, p => p.Name == "My Profile");
    }

    [Fact]
    public void ProfileManager_Duplicate_CopiesEntries()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        var original = mgr.Create("Original");
        original.Entries.Add(new ProfileEntry { RelativePath = "test.dll", Enabled = true });
        var copy = mgr.Duplicate(original);

        Assert.Equal(original.Entries.Count, copy.Entries.Count);
        Assert.NotEqual(original.Id, copy.Id);
    }

    [Fact]
    public void ProfileManager_Delete_RemovesProfile()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        var profile = mgr.Create("ToDelete");
        var before = mgr.Profiles.Count;
        mgr.Delete(profile);

        Assert.Equal(before - 1, mgr.Profiles.Count);
        Assert.DoesNotContain(mgr.Profiles, p => p.Id == profile.Id);
    }

    [Fact]
    public void ProfileManager_SeedsDefaults_WhenEmpty()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        Assert.NotEmpty(mgr.Profiles);
        Assert.Contains(mgr.Profiles, p => p.Name == "Vanilla GTA V");
    }

    // ── Safe Launch ───────────────────────────────────────────────────

    [Fact]
    public void SafeLaunch_BuildPlan_IncludesNonEssentialPlugin()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SomeMod.dll"), "data");

        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        Assert.Contains(plan.Changes, c => c.FilePath.EndsWith("SomeMod.dll"));
    }

    [Fact]
    public void SafeLaunch_BuildPlan_EmptyWhenNoPlugins()
    {
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        Assert.Empty(plan.Changes);
    }

    // ── Restore Points ────────────────────────────────────────────────

    [Fact]
    public async Task RestorePoints_SaveAndReload()
    {
        Directory.CreateDirectory(AppDataPaths.RestorePointsDirectory);
        var svc = new RestorePointService();

        var rp = new RestorePoint { OperationName = "Test Op" };
        rp.Entries.Add(new RestorePointEntry { RelativePath = "plugins/test.dll", WasEnabled = true });
        await svc.SaveAsync(rp);

        var svc2 = new RestorePointService();
        svc2.Load();

        Assert.Contains(svc2.Points, p => p.OperationName == "Test Op");
    }

    [Fact]
    public async Task RestorePoints_Delete_RemovesFromIndex()
    {
        Directory.CreateDirectory(AppDataPaths.RestorePointsDirectory);
        var svc = new RestorePointService();
        var rp = new RestorePoint { OperationName = "ToDelete" };
        await svc.SaveAsync(rp);

        await svc.DeleteAsync(rp);

        var svc2 = new RestorePointService();
        svc2.Load();
        Assert.DoesNotContain(svc2.Points, p => p.Id == rp.Id);
    }

    // ── Setup Wizard ──────────────────────────────────────────────────

    [Fact]
    public void SetupWizard_ValidPath_ReturnsEmptyError()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");

        var error = new SetupWizardService().ValidatePath(_gtaDir);

        Assert.Equal("", error);
    }

    [Fact]
    public void SetupWizard_MissingFolder_ReturnsError()
    {
        var error = new SetupWizardService().ValidatePath(@"C:\DoesNotExist\GTA5");

        Assert.NotEmpty(error);
    }

    [Fact]
    public void SetupWizard_FolderExistsButNoExe_ReturnsError()
    {
        // _gtaDir exists but has no GTA5.exe
        var error = new SetupWizardService().ValidatePath(_gtaDir);

        Assert.Contains("GTA5.exe", error);
    }

    [Fact]
    public void SetupWizard_DetectPaths_DoesNotThrow()
    {
        var candidates = new SetupWizardService().DetectGamePaths();
        Assert.NotNull(candidates);
    }

    // ── Game Version Service ──────────────────────────────────────────

    [Fact]
    public void GameVersion_NullVersion_WhenExeMissing()
    {
        var info = new GameVersionService().GetCurrentVersion();

        Assert.Null(info.Version);
        Assert.False(info.ChangedSinceLastCheck);
    }

    // ── Update Check Service ──────────────────────────────────────────

    [Fact]
    public async Task UpdateCheck_ReturnsValidResult()
    {
        var result = await new UpdateCheckService().CheckAsync();

        Assert.NotNull(result);
        Assert.Equal("3.5.0", result.CurrentVersion);
    }

    // ── Storage Usage Analyzer ────────────────────────────────────────

    [Fact]
    public void StorageAnalyzer_ReportsPluginsFolder()
    {
        var dir = Path.Combine(_gtaDir, "plugins");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.dll"), "content");

        var results = new StorageUsageAnalyzer().Analyze();

        var plugins = results.FirstOrDefault(r => r.Label == "Plugins");
        Assert.NotNull(plugins);
        Assert.True(plugins!.SizeBytes > 0);
    }

    [Fact]
    public void StorageUsageResult_SizeDisplay_FormatsMB()
    {
        var r = new StorageUsageResult { SizeBytes = 2 * 1024 * 1024 };
        Assert.Contains("MB", r.SizeDisplay);
    }

    [Fact]
    public void StorageUsageResult_SizeDisplay_FormatsKB()
    {
        var r = new StorageUsageResult { SizeBytes = 500 };
        Assert.Contains("KB", r.SizeDisplay);
    }

    // ── Disabled Mods Scanner ─────────────────────────────────────────

    [Fact]
    public void DisabledModsScanner_FindsDisabledFiles()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SomeMod.dll.disabled"), "data");

        var results = new DisabledModsScanner().Scan();

        Assert.Contains(results, r => r.OriginalName == "SomeMod.dll");
        Assert.Contains(results, r => r.Category == "LSPDFR Plugin");
    }

    [Fact]
    public void DisabledModsScanner_Enable_RestoresFile()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        var disabled = Path.Combine(dir, "SomeMod.dll.disabled");
        File.WriteAllText(disabled, "data");

        var scanner = new DisabledModsScanner();
        var entry = scanner.Scan().First(r => r.OriginalName == "SomeMod.dll");
        scanner.Enable(entry);

        Assert.True(File.Exists(Path.Combine(dir, "SomeMod.dll")));
        Assert.False(File.Exists(disabled));
    }

    // ── Pre-Launch Checklist ──────────────────────────────────────────

    [Fact]
    public void PreLaunchChecklist_NoBlockers_WhenFullInstall()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(_gtaDir, "RAGEPluginHook.exe"), "");
        Directory.CreateDirectory(Path.Combine(_gtaDir, "plugins"));
        File.WriteAllText(Path.Combine(_gtaDir, "plugins", "LSPDFR.dll"), "");

        var results = new PreLaunchChecklistService().Run(requireLspdfr: true);

        Assert.DoesNotContain(results, r => !r.Passed && r.IsBlocker);
    }

    [Fact]
    public void PreLaunchChecklist_Blocker_WhenGtaFolderMissing()
    {
        AppConfig.Instance.GtaPath = @"C:\DoesNotExist";

        var results = new PreLaunchChecklistService().Run();

        Assert.Contains(results, r => !r.Passed && r.IsBlocker);
    }

    // ── Emergency Recovery ────────────────────────────────────────────

    [Fact]
    public void EmergencyRecovery_DisableAllOptionalPlugins_BuildsActions()
    {
        var dir = Path.Combine(_gtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Extra.dll"),   "data");
        File.WriteAllText(Path.Combine(dir, "Another.dll"), "data");

        var plan = new EmergencyRecoveryService().BuildPlan("DisableAllOptionalPlugins");

        Assert.Equal(2, plan.Actions.Count);
        Assert.All(plan.Actions, a => Assert.True(a.WillDisable));
    }

    [Fact]
    public void EmergencyRecovery_DisableAsi_KeepsEssentialAsi()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "ScriptHookVDotNet.asi"), "");
        File.WriteAllText(Path.Combine(_gtaDir, "SomeMod.asi"),           "");

        var plan = new EmergencyRecoveryService().BuildPlan("DisableAllAsiExceptRequired");

        Assert.Contains(plan.Actions,    a => a.AffectedPath.EndsWith("SomeMod.asi"));
        Assert.DoesNotContain(plan.Actions, a => a.AffectedPath.EndsWith("ScriptHookVDotNet.asi"));
    }

    // ── Settings Validation ───────────────────────────────────────────

    [Fact]
    public void SettingsValidation_Blocker_WhenGtaPathEmpty()
    {
        AppConfig.Instance.GtaPath = "";

        var results = new SettingsValidationService().Validate();

        Assert.Contains(results, r => r.SettingName == "GTA V Path" && r.IsBlocking);
    }

    [Fact]
    public void SettingsValidation_Blocker_WhenGtaFolderMissing()
    {
        AppConfig.Instance.GtaPath = @"C:\DoesNotExist\GTA5";

        var results = new SettingsValidationService().Validate();

        Assert.Contains(results, r => r.SettingName == "GTA V Path" && r.IsBlocking);
    }

    [Fact]
    public void SettingsValidation_NoBlockers_WhenValid()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");
        Directory.CreateDirectory(AppConfig.Instance.BackupPath);

        var results = new SettingsValidationService().Validate();

        Assert.DoesNotContain(results, r => r.IsBlocking);
    }

    // ── Change History ────────────────────────────────────────────────

    [Fact]
    public void ChangeHistory_Record_AddsEntry()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Installed: TestMod", "TestMod.dll");

        Assert.Single(svc.Entries);
        Assert.Equal(ChangeHistoryAction.Installed, svc.Entries[0].Action);
    }

    [Fact]
    public void ChangeHistory_Filter_ByAction()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed,     "Installed A");
        svc.Record(ChangeHistoryAction.BackupCreated, "Backup created");
        svc.Record(ChangeHistoryAction.Installed,     "Installed B");

        var installs = svc.Filter(ChangeHistoryAction.Installed);

        Assert.Equal(2, installs.Count);
        Assert.All(installs, e => Assert.Equal(ChangeHistoryAction.Installed, e.Action));
    }

    [Fact]
    public void ChangeHistory_Filter_BySearch()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Installed: AlphaPlugin");
        svc.Record(ChangeHistoryAction.Installed, "Installed: BetaMod");

        var results = svc.Filter(search: "Alpha");

        Assert.Single(results);
        Assert.Contains("Alpha", results[0].Description);
    }

    [Fact]
    public void ChangeHistory_Clear_RemovesAllEntries()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Test");
        svc.Clear();

        Assert.Empty(svc.Entries);
    }

    // ── Mod Conflict Detector ─────────────────────────────────────────

    [Fact]
    public void ConflictDetector_DetectsMultipleGameconfigs()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "gameconfig.xml"), "<GameConfig/>");
        var modsDir = Path.Combine(_gtaDir, "mods");
        Directory.CreateDirectory(modsDir);
        File.WriteAllText(Path.Combine(modsDir, "gameconfig.xml"), "<GameConfig/>");

        var results = new ModConflictDetector().Detect();

        Assert.Contains(results, r => r.ConflictGroup.Contains("gameconfig", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConflictDetector_NoConflicts_WhenClean()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "GTA5.exe"), "");

        Assert.Empty(new ModConflictDetector().Detect());
    }

    // ── Diagnostics Report Export ─────────────────────────────────────

    [Fact]
    public async Task DiagnosticsOrchestrator_ExportTxt_CreatesFile()
    {
        var findings = new List<DiagnosticFinding>
        {
            new() { Category = "Test", Title = "A finding", Detail = "detail", Severity = DiagnosticSeverity.Warning }
        };
        var outPath = Path.Combine(_tempDir, "report.txt");

        await new DiagnosticsOrchestrator().ExportReportAsync(findings, outPath);

        Assert.True(File.Exists(outPath));
        Assert.Contains("A finding", await File.ReadAllTextAsync(outPath));
    }

    [Fact]
    public async Task DiagnosticsOrchestrator_ExportHtml_ContainsTable()
    {
        var findings = new List<DiagnosticFinding>
        {
            new() { Category = "Deps", Title = "Missing ScriptHookV", Severity = DiagnosticSeverity.Warning }
        };
        var outPath = Path.Combine(_tempDir, "report.html");

        await new DiagnosticsOrchestrator().ExportReportAsync(findings, outPath);

        var content = await File.ReadAllTextAsync(outPath);
        Assert.Contains("<table", content);
        Assert.Contains("Missing ScriptHookV", content);
    }

    // ── Mod Metadata Service ──────────────────────────────────────────

    [Fact]
    public void ModMetadata_SaveAndLoad_RoundTrip()
    {
        Directory.CreateDirectory(Path.Combine(_appDataDir, "data"));
        var svc = new ModMetadataService();
        svc.Load();

        var meta = svc.GetOrCreate("mod-1");
        meta.CustomName = "Custom Name";
        meta.Tags.Add("police");
        svc.Save(meta);

        var svc2 = new ModMetadataService();
        svc2.Load();
        var loaded = svc2.GetOrCreate("mod-1");

        Assert.Equal("Custom Name", loaded.CustomName);
        Assert.Contains("police", loaded.Tags);
    }

    // ── Loadout Manifest ──────────────────────────────────────────────

    [Fact]
    public async Task LoadoutManifest_ExportAndImport_RoundTrip()
    {
        var outPath = Path.Combine(_tempDir, "loadout.lspmanifest");
        var svc = new LoadoutManifestService();

        await svc.ExportToFileAsync(outPath);
        Assert.True(File.Exists(outPath));

        var imported = await svc.ImportFromFileAsync(outPath);
        Assert.NotNull(imported);
        Assert.Equal("3.5.0", imported!.ManagerVersion);
    }

    // ── Log Viewer Service ────────────────────────────────────────────

    [Fact]
    public void LogViewer_Search_FiltersLines()
    {
        var svc = new LogViewerService();
        var lines = new[] { "[INFO] All good", "[ERROR] something failed", "[INFO] Another line" };

        var results = svc.Search(lines, "ERROR");

        Assert.Single(results);
        Assert.Contains("ERROR", results[0]);
    }

    [Fact]
    public void LogViewer_Search_EmptyQuery_ReturnsAll()
    {
        var svc = new LogViewerService();
        var lines = new[] { "line1", "line2", "line3" };

        Assert.Equal(3, svc.Search(lines, "").Length);
    }

    [Fact]
    public void LogViewer_ReadLines_ReturnsEmpty_ForMissingFile()
    {
        var lines = new LogViewerService().ReadLines(@"C:\DoesNotExist\nope.log");
        Assert.Empty(lines);
    }
}
