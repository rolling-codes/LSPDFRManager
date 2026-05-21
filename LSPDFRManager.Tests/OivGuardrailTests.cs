using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class OivGuardrailTests : CommandCenterTestBase
{
    private string CreateArchive(params (string path, string content)[] entries)
    {
        var zipPath = Path.Combine(TempDir, $"oiv_{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            var entry = zip.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return zipPath;
    }

    // ── Primary OIV ──────────────────────────────────────────────────────────

    // Canonical OIV archive: assembly.xml at root + a binary content file.
    // No DLC/script paths to confuse the classifier.
    private string CreateOivArchive() => CreateArchive(
        ("assembly.xml", "<package><metadata /><content /></package>"),
        ("content/somebinary.rpf", "data")
    );

    [Fact]
    public void OivPrimary_BlockingIssueAdded()
    {
        var plan = new SmartInstallPlanner().BuildPlan(CreateOivArchive());
        Assert.Contains(plan.BlockingIssues, b => b.Contains("OIV package"));
    }

    [Fact]
    public void OivPrimary_RequiresManualConfirmation()
    {
        var plan = new SmartInstallPlanner().BuildPlan(CreateOivArchive());
        Assert.True(plan.RequiresManualConfirmation);
    }

    [Fact]
    public void OivPrimary_BlockMessageContainsOpenIVGuidance()
    {
        var plan = new SmartInstallPlanner().BuildPlan(CreateOivArchive());
        Assert.Contains(plan.BlockingIssues,
            b => b.Contains("OpenIV", StringComparison.OrdinalIgnoreCase));
    }

    // ── Non-OIV unaffected ────────────────────────────────────────────────────

    [Fact]
    public void NonOiv_NoBlockingIssueForOiv()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "data")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.DoesNotContain(plan.BlockingIssues,
            b => b.Contains("OIV", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonOiv_ConfirmNotBlocked()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "data")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        // BlockingIssues drives ReviewCanConfirm; no OIV block means it stays clear
        Assert.DoesNotContain(plan.BlockingIssues,
            b => b.Contains("OIV", StringComparison.OrdinalIgnoreCase));
    }

    // ── InstallerSafetyPolicy constants ──────────────────────────────────────

    [Fact]
    public void OivPrimaryBlockMessage_MentionsOpenIV()
    {
        Assert.Contains("OpenIV", InstallerSafetyPolicy.OivPrimaryBlockMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OivSecondaryWarningMessage_MentionsOpenIV()
    {
        Assert.Contains("OpenIV", InstallerSafetyPolicy.OivSecondaryWarningMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── ViewModel property logic (via InstallPlan shape) ─────────────────────

    // ReviewIsOivPrimary and ReviewIsOivSecondary are pure expressions over
    // ModTypeDetectionResult. We verify the underlying plan shape the VM reads
    // rather than setting the private ReviewPlan setter from outside the VM.

    [Fact]
    public void PlanWithOivPrimary_HasModTypeResultWithOivPrimary()
    {
        var plan = new SmartInstallPlanner().BuildPlan(CreateOivArchive());
        Assert.Equal(ModType.OivPackage, plan.ModTypeResult?.PrimaryType);
    }

    [Fact]
    public void PlanWithOivPrimary_HasNoOivSecondary()
    {
        var plan = new SmartInstallPlanner().BuildPlan(CreateOivArchive());
        var secondaryOiv = plan.ModTypeResult?.SecondaryTypes
            .Any(t => t.Type == ModType.OivPackage) ?? false;
        Assert.False(secondaryOiv);
    }

    [Fact]
    public void PlanWithLspdfrPlugin_HasNoOivPrimary()
    {
        var archive = CreateArchive(
            ("plugins/lspdfr/MyPlugin.dll", "data")
        );

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.NotEqual(ModType.OivPackage, plan.ModTypeResult?.PrimaryType);
    }
}
