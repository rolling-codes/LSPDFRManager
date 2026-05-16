using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Verifies that SmartInstallPlanner produces review-plan groupings that match
/// what InstallViewModel exposes in the pre-install review panel.
/// </summary>
public class InstallReviewPlanTests : CommandCenterTestBase
{
    private string CreateArchive(params (string path, string content)[] entries)
    {
        var zipPath = Path.Combine(TempDir, $"review_{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            var e = zip.CreateEntry(path);
            using var w = new StreamWriter(e.Open());
            w.Write(content);
        }
        return zipPath;
    }

    // ── Clean archive ────────────────────────────────────────────────────────

    [Fact]
    public void CleanArchive_AllEntriesInWillInstall()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "dll"),
            ("plugins/lspdfr/MyPlugin.ini", "[Main]"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var willInstall = plan.Entries.Where(e =>
            e.Risk != InstallRisk.Incompatible &&
            !e.WillOverwrite &&
            e.PlannedAction != InstallConflictAction.Skip &&
            !InstallerSafetyPolicy.IsJunkEntry(e.ArchivePath)).ToList();

        Assert.Equal(2, willInstall.Count);
        Assert.Empty(plan.BlockingIssues);
    }

    // ── Nested root archive ──────────────────────────────────────────────────

    [Fact]
    public void NestedRootArchive_JunkFilteredFromReview()
    {
        var archive = CreateArchive(
            ("LSPDFR_5.1/__MACOSX/._readme.txt", "junk"),
            ("LSPDFR_5.1/plugins/lspdfr/LSPDFR.dll", "dll"),
            ("LSPDFR_5.1/auto-error-cache/crash.log", "log"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var junk = plan.Entries.Where(e => InstallerSafetyPolicy.IsJunkEntry(e.ArchivePath)).ToList();
        Assert.True(junk.Count >= 2);
    }

    // ── Archive with unsafe paths ────────────────────────────────────────────

    [Fact]
    public void UnsafePath_AppearsAsIncompatibleRisk()
    {
        var archive = CreateArchive(
            ("plugins/safe.dll", "dll"),
            ("../../escape.exe", "bad"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var blocked = plan.Entries.Where(e => e.Risk == InstallRisk.Incompatible).ToList();
        Assert.Single(blocked);
        Assert.Contains(plan.Warnings, w => w.Contains("Suspicious path", StringComparison.OrdinalIgnoreCase));
    }

    // ── Archive with executable ──────────────────────────────────────────────

    [Fact]
    public void ExecutableEntry_AppearsAsSuspicious()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "dll"),
            ("Installer.exe", "exe"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var suspicious = plan.Entries.Where(e => e.Risk == InstallRisk.Suspicious).ToList();
        Assert.Single(suspicious);
        Assert.Contains(plan.Warnings, w => w.Contains(".exe", StringComparison.OrdinalIgnoreCase));
    }

    // ── Archive with overwrite ───────────────────────────────────────────────

    [Fact]
    public void ExistingFile_AppearsAsWillOverwrite()
    {
        var existing = Path.Combine(GtaDir, "plugins", "lspdfr", "MyPlugin.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "old");

        var archive = CreateArchive(("plugins/lspdfr/MyPlugin.dll", "new"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var overwrite = plan.Entries.Where(e => e.WillOverwrite && e.Risk != InstallRisk.Incompatible).ToList();
        Assert.Single(overwrite);
    }

    // ── Mixed Required/Optional/NeedsReview entries ──────────────────────────

    [Fact]
    public void MixedArchive_BlockingIssueDisablesConfirm()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/UltimateBackup.dll", "ub"),
            ("plugins/lspdfr/UltimateBackup/backup.xml", "<Unit Type=\"Coroner\" />"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.True(plan.RequiresManualConfirmation);
        Assert.NotEmpty(plan.BlockingIssues);
    }

    [Fact]
    public void CleanArchive_NoBlockingIssues_ConfirmEnabled()
    {
        var archive = CreateArchive(("plugins/lspdfr/MyPlugin.dll", "dll"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.False(plan.RequiresManualConfirmation);
        Assert.Empty(plan.BlockingIssues);
    }

    // ── Junk-only sections of a mixed archive ────────────────────────────────

    [Fact]
    public void JunkDirectories_AreRecognisedAsJunk()
    {
        var junkPaths = new[]
        {
            "__macosx/._MyPlugin.dll",
            "auto-error-cache/crash.log",
            "logs/install.log",
            "temp/working.tmp",
        };

        foreach (var path in junkPaths)
            Assert.True(InstallerSafetyPolicy.IsJunkEntry(path),
                $"Expected '{path}' to be classified as junk");
    }

    [Fact]
    public void LegitimatePluginPaths_AreNotJunk()
    {
        var legitPaths = new[]
        {
            "plugins/lspdfr/MyPlugin.dll",
            "plugins/lspdfr/MyPlugin.ini",
            "lspdfr/data/config.xml",
            "scripts/MyScript.dll",
        };

        foreach (var path in legitPaths)
            Assert.False(InstallerSafetyPolicy.IsJunkEntry(path),
                $"Expected '{path}' NOT to be classified as junk");
    }

    // ── Cancel does not write files ──────────────────────────────────────────

    [Fact]
    public void BuildPlan_DoesNotWriteAnyFiles()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "dll"),
            ("plugins/lspdfr/MyPlugin.ini", "[Main]"));

        var filesBefore = Directory.GetFiles(GtaDir, "*", SearchOption.AllDirectories);

        new SmartInstallPlanner().BuildPlan(archive);

        var filesAfter = Directory.GetFiles(GtaDir, "*", SearchOption.AllDirectories);
        Assert.Equal(filesBefore.Length, filesAfter.Length);
    }

    // ── Readme is captured ───────────────────────────────────────────────────

    [Fact]
    public void ArchiveWithReadme_CapturesReadmeContent()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "dll"),
            ("readme.txt", "Install this great plugin!"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.NotNull(plan.ReadmeContent);
        Assert.Contains("Install this great plugin!", plan.ReadmeContent);
    }

    // ── Empty archive ────────────────────────────────────────────────────────

    [Fact]
    public void EmptyArchive_ProducesEmptyPlan()
    {
        var archive = CreateArchive();

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.Empty(plan.Entries);
        Assert.Empty(plan.BlockingIssues);
    }
}
