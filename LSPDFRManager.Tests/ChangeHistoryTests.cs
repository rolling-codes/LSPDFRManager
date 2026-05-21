using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class ChangeHistoryTests : CommandCenterTestBase
{
    [Fact]
    public void Record_AddsEntry()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Installed: TestMod", "TestMod.dll");

        Assert.Single(svc.Entries);
        Assert.Equal(ChangeHistoryAction.Installed, svc.Entries[0].Action);
    }

    [Fact]
    public void Filter_ByAction()
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
    public void Filter_BySearch()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Installed: AlphaPlugin");
        svc.Record(ChangeHistoryAction.Installed, "Installed: BetaMod");

        var results = svc.Filter(search: "Alpha");

        Assert.Single(results);
        Assert.Contains("Alpha", results[0].Description);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var svc = new ChangeHistoryService();
        svc.Record(ChangeHistoryAction.Installed, "Test");
        svc.Clear();

        Assert.Empty(svc.Entries);
    }

    [Fact]
    public void DisabledModsScanner_FindsDisabledFiles()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SomeMod.dll.disabled"), "data");

        var results = new DisabledModsScanner().Scan();

        Assert.Contains(results, r => r.OriginalName == "SomeMod.dll");
        Assert.Contains(results, r => r.Category == "LSPDFR Plugin");
    }

    [Fact]
    public void DisabledModsScanner_Enable_RestoresFile()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        var disabled = Path.Combine(dir, "SomeMod.dll.disabled");
        File.WriteAllText(disabled, "data");

        var scanner = new DisabledModsScanner();
        var entry = scanner.Scan().First(r => r.OriginalName == "SomeMod.dll");
        scanner.Enable(entry);

        Assert.True(File.Exists(Path.Combine(dir, "SomeMod.dll")));
        Assert.False(File.Exists(disabled));
    }
}
