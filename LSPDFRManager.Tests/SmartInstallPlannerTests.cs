using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class SmartInstallPlannerTests : CommandCenterTestBase
{
    private string CreateArchive(params (string path, string? content)[] entries)
    {
        var zipPath = Path.Combine(TempDir, $"planner_{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            var entry = zip.CreateEntry(path);
            if (content is null)
                continue;

            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return zipPath;
    }

    [Fact]
    public void BatchWithStopThePedAndUltimateBackup_OrdersStopThePedFirst()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/UltimateBackup.dll", "ub"),
            ("plugins/lspdfr/UltimateBackup/DefaultRegions.xml", "<Regions />"),
            ("plugins/lspdfr/StopThePed.dll", "stp"),
            ("plugins/lspdfr/StopThePed.ini", "[Main]")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var stopDllIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("StopThePed.dll", StringComparison.OrdinalIgnoreCase));
        var stopConfigIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("StopThePed.ini", StringComparison.OrdinalIgnoreCase));
        var ubDllIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("UltimateBackup.dll", StringComparison.OrdinalIgnoreCase));
        var ubConfigIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("DefaultRegions.xml", StringComparison.OrdinalIgnoreCase));

        Assert.True(stopDllIndex >= 0);
        Assert.True(stopConfigIndex >= 0);
        Assert.True(ubDllIndex >= 0);
        Assert.True(ubConfigIndex >= 0);
        Assert.True(stopDllIndex < ubDllIndex);
        Assert.True(stopConfigIndex < ubConfigIndex);

        Assert.Equal("Stop The Ped", plan.InstallOrder.First(o => o == "Stop The Ped"));
        Assert.Equal("Ultimate Backup", plan.InstallOrder.Last(o => o == "Ultimate Backup"));
    }

    [Fact]
    public void BatchWithUltimateBackupOnly_WarnsButDoesNotBlock()
    {
        var archive = CreateArchive(("plugins/lspdfr/UltimateBackup.dll", "ub"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.Contains(plan.Warnings, w => w.Contains("Ultimate Backup detected", StringComparison.OrdinalIgnoreCase));
        Assert.False(plan.RequiresManualConfirmation);
    }

    [Fact]
    public void BatchWithUltimateBackupOnlyAndStopThePedAlreadyInstalled_DoesNotWarnMissingDependency()
    {
        var stpPath = Path.Combine(GtaDir, "plugins", "lspdfr", "StopThePed.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(stpPath)!);
        File.WriteAllText(stpPath, "stp");

        var archive = CreateArchive(("plugins/lspdfr/UltimateBackup.dll", "ub"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.DoesNotContain(plan.Warnings, w => w.Contains("Ultimate Backup detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedDependenciesAreScheduledBeforePluginDlls()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/UltimateBackup.dll", "ub"),
            ("plugins/lspdfr/StopThePed.dll", "stp"),
            ("plugins/lspdfr/LemonUI.SHVDN3.dll", "dep")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var dependencyIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("LemonUI.SHVDN3.dll", StringComparison.OrdinalIgnoreCase));
        var stopIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("StopThePed.dll", StringComparison.OrdinalIgnoreCase));
        var ultimateIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("UltimateBackup.dll", StringComparison.OrdinalIgnoreCase));

        Assert.True(dependencyIndex >= 0 && stopIndex >= 0 && ultimateIndex >= 0);
        Assert.True(dependencyIndex < stopIndex);
        Assert.True(dependencyIndex < ultimateIndex);
    }

    [Fact]
    public void UnrelatedPluginOrderRemainsStable()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/ExampleA.dll", "a"),
            ("plugins/lspdfr/ExampleB.dll", "b")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var aIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("ExampleA.dll", StringComparison.OrdinalIgnoreCase));
        var bIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("ExampleB.dll", StringComparison.OrdinalIgnoreCase));

        Assert.True(aIndex >= 0 && bIndex >= 0);
        Assert.True(aIndex < bIndex);
    }

    [Fact]
    public void ExistingBackupConfigsAreConflictsAndNotDefaultReplace()
    {
        var existingUltimateBackupIni = Path.Combine(GtaDir, "plugins", "lspdfr", "UltimateBackup.ini");
        var existingDefaultRegions = Path.Combine(GtaDir, "plugins", "lspdfr", "UltimateBackup", "DefaultRegions.xml");
        var existingBackupXml = Path.Combine(GtaDir, "lspdfr", "data", "backup.xml");
        var existingAgencyXml = Path.Combine(GtaDir, "lspdfr", "data", "agency.xml");
        var existingRegionsXml = Path.Combine(GtaDir, "lspdfr", "data", "regions.xml");
        var existingCustomRegionsXml = Path.Combine(GtaDir, "lspdfr", "data", "customregions.xml");

        Directory.CreateDirectory(Path.GetDirectoryName(existingUltimateBackupIni)!);
        Directory.CreateDirectory(Path.GetDirectoryName(existingDefaultRegions)!);
        Directory.CreateDirectory(Path.GetDirectoryName(existingBackupXml)!);

        File.WriteAllText(existingUltimateBackupIni, "old");
        File.WriteAllText(existingDefaultRegions, "old");
        File.WriteAllText(existingBackupXml, "old");
        File.WriteAllText(existingAgencyXml, "old");
        File.WriteAllText(existingRegionsXml, "old");
        File.WriteAllText(existingCustomRegionsXml, "old");

        var archive = CreateArchive(
            ("plugins/lspdfr/UltimateBackup.ini", "new"),
            ("plugins/lspdfr/UltimateBackup/DefaultRegions.xml", "<new />"),
            ("lspdfr/data/backup.xml", "<new />"),
            ("lspdfr/data/agency.xml", "<new />"),
            ("lspdfr/data/regions.xml", "<new />"),
            ("lspdfr/data/customregions.xml", "<new />")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.All(plan.Entries, entry =>
        {
            Assert.True(entry.DestinationExists);
            Assert.True(entry.OverwriteRisk >= InstallOverwriteRisk.High);
            Assert.NotEqual(InstallConflictAction.BackupAndReplace, entry.PlannedAction);
        });
    }

    [Fact]
    public void ExistingPluginDllIsDetectedAsOverwriteConflict()
    {
        var existingDll = Path.Combine(GtaDir, "plugins", "lspdfr", "UltimateBackup.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(existingDll)!);
        File.WriteAllText(existingDll, "old");

        var archive = CreateArchive(("plugins/lspdfr/UltimateBackup.dll", "new"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);
        var entry = Assert.Single(plan.Entries);

        Assert.True(entry.DestinationExists);
        Assert.Equal(InstallRisk.Overwrite, entry.Risk);
        Assert.True(entry.OverwriteRisk >= InstallOverwriteRisk.High);
    }

    [Fact]
    public void ConfigWithTransportReferenceAndMissingStopThePed_RequiresManualConfirmation()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/UltimateBackup.dll", "ub"),
            ("plugins/lspdfr/UltimateBackup/backup.xml", "<Unit Type=\"Coroner\" />")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.True(plan.RequiresManualConfirmation);
        Assert.Contains(plan.BlockingIssues, issue => issue.Contains("transport/coroner", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void DependencySorting_DoesNotBypassOverwriteConflictDetection()
    {
        var existing = Path.Combine(GtaDir, "plugins", "lspdfr", "UltimateBackup.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "old");

        var archive = CreateArchive(
            ("plugins/lspdfr/StopThePed.dll", "stp"),
            ("plugins/lspdfr/UltimateBackup.dll", "ub"),
            ("plugins/lspdfr/UltimateBackup.ini", "new")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var configEntry = Assert.Single(plan.Entries.Where(e => e.ArchivePath.EndsWith("UltimateBackup.ini", StringComparison.OrdinalIgnoreCase)));
        Assert.True(configEntry.DestinationExists);
        Assert.True(configEntry.OverwriteRisk >= InstallOverwriteRisk.High);
    }

}
