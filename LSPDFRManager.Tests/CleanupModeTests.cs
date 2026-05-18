using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.Services.Modes;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("CommandCenter")]
public class CleanupModeTests : CommandCenterTestBase
{


    // Test 8
    [Fact]
    public void SafeCoreReset_SelectsCoreDll_NotPluginsLspdfrContents()
    {
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins"));
        File.WriteAllText(Path.Combine(GtaDir, "plugins", "LSPD First Response.dll"), "");

        var pluginDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "StopThePed.dll"), "");

        var scan = LspdfrCleanupScanner.Scan(GtaDir);
        var preset = new SafeCoreResetMode().Apply(scan);

        // LSPDFR core DLL is default-selected
        var coreCandidates = scan.Groups
            .Where(g => g.GroupKind == CandidateClassification.LspdfrCore)
            .SelectMany(g => g.Candidates);
        Assert.All(coreCandidates, c => Assert.Contains(c.Id, preset.DefaultSelectedIds));

        // Third-party plugin is NOT default-selected
        var pluginCandidates = scan.Groups
            .Where(g => g.GroupKind == CandidateClassification.ThirdPartyPlugin)
            .SelectMany(g => g.Candidates);
        Assert.All(pluginCandidates, c => Assert.DoesNotContain(c.Id, preset.DefaultSelectedIds));
    }

    // Test 9
    [Fact]
    public void SelectedThirdPartyPluginCleanup_DefaultsToNothingSelected()
    {
        var pluginDir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "StopThePed.dll"), "");
        File.WriteAllText(Path.Combine(pluginDir, "UltimateBackup.dll"), "");

        var scan = LspdfrCleanupScanner.Scan(GtaDir);
        var preset = new SelectedThirdPartyPluginCleanupMode().Apply(scan);

        Assert.Empty(preset.DefaultSelectedIds);
        Assert.Equal("DELETE SELECTED PLUGINS", preset.ConfirmPhrase);
    }
}
