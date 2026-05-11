using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class PluginHealthScannerTests : CommandCenterTestBase
{
    [Fact]
    public void NoIssues_WhenFolderEmpty()
    {
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins", "lspdfr"));
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Severity == PluginScanSeverity.Ok);
    }

    [Fact]
    public void DetectsDuplicateDll()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "TestMod.dll"),          "data");
        File.WriteAllText(Path.Combine(dir, "TestMod.dll.disabled"), "data");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("Duplicate") && r.FileName.Contains("TestMod"));
    }

    [Fact]
    public void DetectsZeroByteDll()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Empty.dll"), "");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("Zero-byte") && r.FileName == "Empty.dll");
    }

    [Fact]
    public void DetectsZipInsidePluginsFolder()
    {
        var dir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "SomeMod.zip"), "PK...");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("archive found inside GTA V folder"));
    }

    [Fact]
    public void DetectsDisabledFile()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "");
        File.WriteAllText(Path.Combine(dir, "Plugin.dll.disabled"), "data");

        var results = new PluginHealthScanner().Scan();

        Assert.Contains(results, r => r.Issue.Contains("disabled") && r.Severity == PluginScanSeverity.Info);
    }
}
