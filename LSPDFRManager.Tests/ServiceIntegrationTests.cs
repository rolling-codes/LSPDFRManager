using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Integration tests for services that don't have a dedicated file:
/// EmergencyRecovery, SettingsValidation, PreLaunchChecklist,
/// ModMetadata, LoadoutManifest, LogViewer.
/// </summary>
public class ServiceIntegrationTests : CommandCenterTestBase
{
    [Fact]
    public void EmergencyRecovery_DisableAllOptionalPlugins_BuildsActions()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
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
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookVDotNet.asi"), "");
        File.WriteAllText(Path.Combine(GtaDir, "SomeMod.asi"),           "");

        var plan = new EmergencyRecoveryService().BuildPlan("DisableAllAsiExceptRequired");

        Assert.Contains(plan.Actions,       a => a.AffectedPath.EndsWith("SomeMod.asi"));
        Assert.DoesNotContain(plan.Actions, a => a.AffectedPath.EndsWith("ScriptHookVDotNet.asi"));
    }

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
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        Directory.CreateDirectory(AppConfig.Instance.BackupPath);

        var results = new SettingsValidationService().Validate();

        Assert.DoesNotContain(results, r => r.IsBlocking);
    }

    [Fact]
    public void PreLaunchChecklist_NoBlockers_WhenFullInstall()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "");
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins"));
        File.WriteAllText(Path.Combine(GtaDir, "plugins", "LSPDFR.dll"), "");

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

    [Fact]
    public void ModMetadata_SaveAndLoad_RoundTrip()
    {
        Directory.CreateDirectory(Path.Combine(AppDataDir, "data"));
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

    [Fact]
    public async Task LoadoutManifest_ExportAndImport_RoundTrip()
    {
        var outPath = Path.Combine(TempDir, "loadout.lspmanifest");
        var svc = new LoadoutManifestService();

        await svc.ExportToFileAsync(outPath);
        Assert.True(File.Exists(outPath));

        var imported = await svc.ImportFromFileAsync(outPath);
        Assert.NotNull(imported);
        Assert.Equal("3.6.0", imported!.ManagerVersion);
    }

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
