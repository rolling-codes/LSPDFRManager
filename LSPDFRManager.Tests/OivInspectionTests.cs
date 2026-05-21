using System.IO.Compression;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class OivInspectionTests : CommandCenterTestBase
{
    private string CreateOivArchive(string assemblyXml, params (string path, string content)[] extraFiles)
    {
        var zipPath = Path.Combine(TempDir, $"oiv_insp_{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        var xmlEntry = zip.CreateEntry("assembly.xml");
        using (var w = new StreamWriter(xmlEntry.Open()))
            w.Write(assemblyXml);

        foreach (var (path, content) in extraFiles)
        {
            var e = zip.CreateEntry(path);
            using var w = new StreamWriter(e.Open());
            w.Write(content);
        }

        return zipPath;
    }

    private const string ValidAssemblyXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <package version="2.0">
          <metadata>
            <name>My Test Mod</name>
            <version><major>2</major><minor>3</minor></version>
            <author>TestAuthor</author>
            <description>A test OIV mod.</description>
          </metadata>
          <content>
            <add source="content/update/x64/dlcpacks/mymod/dlc.rpf">update/x64/dlcpacks/mymod/dlc.rpf</add>
            <add source="content/common/data/mymod.meta">common/data/mymod.meta</add>
          </content>
        </package>
        """;

    // ── ParseFromStream — valid manifest ──────────────────────────────────────

    [Fact]
    public void ParseFromStream_ValidManifest_ReturnsValidPackage()
    {
        var xml = ValidAssemblyXml;
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.True(pkg.IsValid);
        Assert.Equal("My Test Mod", pkg.Name);
        Assert.Equal("2.3", pkg.Version);
        Assert.Equal("TestAuthor", pkg.Author);
        Assert.Equal(2, pkg.Files.Count);
    }

    [Fact]
    public void ParseFromStream_ValidManifest_FilePathsPopulated()
    {
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(ValidAssemblyXml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.Contains(pkg.Files, f => f.InstallPath == "update/x64/dlcpacks/mymod/dlc.rpf");
        Assert.Contains(pkg.Files, f => f.InstallPath == "common/data/mymod.meta");
    }

    [Fact]
    public void ParseFromStream_DefaultsTargetGame_WhenNotDeclared()
    {
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(ValidAssemblyXml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.Equal("Grand Theft Auto V", pkg.TargetGame);
    }

    [Fact]
    public void ParseFromStream_ReadsTargetGame_WhenDeclared()
    {
        var xml = ValidAssemblyXml.Replace(
            "<description>A test OIV mod.</description>",
            "<description>A test OIV mod.</description><targetGame>GTA V 1.0.3179</targetGame>");
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.Equal("GTA V 1.0.3179", pkg.TargetGame);
    }

    // ── ParseFromStream — malformed / missing elements ────────────────────────

    [Fact]
    public void ParseFromStream_MalformedXml_ReturnsInvalidPackage()
    {
        var badXml = "<package><metadata><name>Broken</name></metadata";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(badXml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.False(pkg.IsValid);
        Assert.NotNull(pkg.ValidationError);
    }

    [Fact]
    public void ParseFromStream_WrongRootElement_ReturnsInvalidPackage()
    {
        var xml = "<notapackage><metadata /></notapackage>";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.False(pkg.IsValid);
    }

    [Fact]
    public void ParseFromStream_MissingMetadata_ReturnsInvalidPackage()
    {
        var xml = "<package version=\"2.0\"><content /></package>";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.False(pkg.IsValid);
    }

    [Fact]
    public void ParseFromStream_EmptyContent_ReturnsZeroFiles()
    {
        var xml = "<package><metadata><name>N</name><author>A</author><description/></metadata><content /></package>";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var pkg = OivService.ParseFromStream(stream);

        Assert.True(pkg.IsValid);
        Assert.Empty(pkg.Files);
    }

    // ── SmartInstallPlanner integration ──────────────────────────────────────

    [Fact]
    public void Planner_OivPrimary_PopulatesOivMetadata()
    {
        var archive = CreateOivArchive(ValidAssemblyXml,
            ("content/update/x64/dlcpacks/mymod/dlc.rpf", "data"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.NotNull(plan.OivMetadata);
        Assert.True(plan.OivMetadata!.IsValid);
        Assert.Equal("My Test Mod", plan.OivMetadata.Name);
        Assert.Equal("2.3", plan.OivMetadata.Version);
        Assert.Equal("TestAuthor", plan.OivMetadata.Author);
    }

    [Fact]
    public void Planner_OivPrimary_OivMetadataFileCountMatchesManifest()
    {
        var archive = CreateOivArchive(ValidAssemblyXml,
            ("content/update/x64/dlcpacks/mymod/dlc.rpf", "data"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.Equal(2, plan.OivMetadata?.Files.Count);
    }

    [Fact]
    public void Planner_OivPrimary_MalformedAssemblyXml_OivMetadataNull()
    {
        // Malformed assembly.xml — parser returns invalid package, planner stores null
        var archive = CreateOivArchive("<not valid xml<<<",
            ("content/file.dat", "data"));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        // Still blocked (type detected), but metadata is null or invalid
        Assert.Contains(plan.BlockingIssues, b => b.Contains("OIV package"));
        Assert.True(plan.OivMetadata is null || !plan.OivMetadata.IsValid);
    }

    [Fact]
    public void Planner_NonOiv_OivMetadataIsNull()
    {
        var zipPath = Path.Combine(TempDir, $"nonOiv_{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var e = zip.CreateEntry("plugins/lspdfr/MyPlugin.dll");
            using var w = new StreamWriter(e.Open());
            w.Write("data");
        }

        var plan = new SmartInstallPlanner().BuildPlan(zipPath);

        Assert.Null(plan.OivMetadata);
    }
}
