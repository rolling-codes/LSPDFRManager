using System.IO.Compression;
using System.Text;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for the OIV Creator pipeline:
/// OivSourceScanner → OivPackagePlan → OivPackageValidator → OivAssemblyXmlWriter → OivPackageBuilder
/// </summary>
public class OivCreatorTests : CommandCenterTestBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private string WriteFile(string name, string content = "data")
    {
        var path = Path.Combine(TempDir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static OivPackagePlan Template(string name = "Test Mod") => new()
    {
        Name    = name,
        Version = "1.0",
        Author  = "Tester",
    };

    // ── OivSourceScanner ─────────────────────────────────────────────────────

    [Fact]
    public void Scanner_SingleFile_AddsOneEntry()
    {
        var f       = WriteFile("myplugin.dll");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([f], Template());

        Assert.Single(plan.Files);
        Assert.Equal("myplugin.dll", plan.Files[0].InstallPath);
        Assert.Equal(f, plan.Files[0].SourcePath);
    }

    [Fact]
    public void Scanner_Folder_AddsAllFiles()
    {
        var dir = Path.Combine(TempDir, "src");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.dll"), "x");
        File.WriteAllText(Path.Combine(dir, "b.ini"), "y");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([dir], Template());

        Assert.Equal(2, plan.Files.Count);
    }

    [Fact]
    public void Scanner_FolderSubdirectory_UsesRelativePaths()
    {
        var dir = Path.Combine(TempDir, "src2");
        var sub = Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "c.xml"), "data");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([dir], Template());

        Assert.Contains(plan.Files, f => f.InstallPath == "sub/c.xml");
    }

    [Fact]
    public void Scanner_BlocksExeFile_AddsWarning()
    {
        var exe     = WriteFile("hack.exe");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([exe], Template());

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Warnings, w => w.Contains(".exe"));
    }

    [Fact]
    public void Scanner_BlocksBatFile_AddsWarning()
    {
        var bat     = WriteFile("run.bat");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([bat], Template());

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Warnings, w => w.Contains(".bat"));
    }

    [Fact]
    public void Scanner_MissingPath_AddsWarning()
    {
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan(["/nonexistent/file.dll"], Template());

        Assert.Empty(plan.Files);
        Assert.NotEmpty(plan.Warnings);
    }

    [Fact]
    public void Scanner_PreservesTemplateMetadata()
    {
        var f       = WriteFile("x.dll");
        var scanner = new OivSourceScanner();

        var plan = scanner.Scan([f], Template("My Package"));

        Assert.Equal("My Package", plan.Name);
        Assert.Equal("1.0", plan.Version);
        Assert.Equal("Tester", plan.Author);
    }

    // ── OivPackageValidator ───────────────────────────────────────────────────

    [Fact]
    public void Validator_ValidPlan_NoErrors()
    {
        var f    = WriteFile("plugin.dll");
        var plan = new OivPackagePlan
        {
            Name    = "My Mod",
            Version = "1.0",
            Files   = [new OivPackageFile(f, "plugin.dll", 4)]
        };
        var v = new OivPackageValidator();

        var result = v.Validate(plan);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validator_EmptyName_AddsError()
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name  = "",
            Files = [new OivPackageFile(f, "x.dll", 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validator_NoFiles_AddsError()
    {
        var plan = new OivPackagePlan { Name = "Mod", Files = [] };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least one file"));
    }

    [Fact]
    public void Validator_DuplicateInstallPaths_AddsError()
    {
        var f1 = WriteFile("a.dll");
        var f2 = WriteFile("b.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files =
            [
                new OivPackageFile(f1, "plugins/mod.dll", 4),
                new OivPackageFile(f2, "plugins/mod.dll", 4)
            ]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validator_PathTraversal_AddsError()
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, "../etc/passwd", 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("traversal"));
    }

    [Fact]
    public void Validator_MissingSourceFile_AddsError()
    {
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile("/nonexistent/file.dll", "file.dll", 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no longer exists"));
    }

    // ── OivAssemblyXmlWriter ──────────────────────────────────────────────────

    [Fact]
    public void XmlWriter_ValidPlan_ContainsPackageName()
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name    = "My Mod",
            Version = "2.1",
            Author  = "Dev",
            Files   = [new OivPackageFile(f, "x.dll", 4)]
        };
        var writer = new OivAssemblyXmlWriter();

        var xml = writer.Write(plan);

        Assert.Contains("<name>My Mod</name>", xml);
        Assert.Contains("<major>2</major>", xml);
        Assert.Contains("<minor>1</minor>", xml);
        Assert.Contains("<author>Dev</author>", xml);
    }

    [Fact]
    public void XmlWriter_TargetIsFive()
    {
        var f    = WriteFile("y.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, "y.dll", 4)]
        };

        var xml = new OivAssemblyXmlWriter().Write(plan);

        Assert.Contains("target=\"Five\"", xml);
    }

    [Fact]
    public void XmlWriter_ContentEntryHasCorrectPaths()
    {
        var f    = WriteFile("z.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, "plugins/lspdfr/z.dll", 4)]
        };

        var xml = new OivAssemblyXmlWriter().Write(plan);

        Assert.Contains("content/plugins/lspdfr/z.dll", xml);
        Assert.Contains(">plugins/lspdfr/z.dll<", xml);
    }

    [Fact]
    public void XmlWriter_EscapesSpecialCharacters()
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name        = "Mod & Test <cool>",
            Author      = "A\"B",
            Description = "A < B > C & D",
            Files       = [new OivPackageFile(f, "x.dll", 4)]
        };

        var xml = new OivAssemblyXmlWriter().Write(plan);

        Assert.Contains("Mod &amp; Test &lt;cool&gt;", xml);
        Assert.DoesNotContain("<cool>", xml);
    }

    // ── OivPackageBuilder ─────────────────────────────────────────────────────

    [Fact]
    public async Task Builder_ValidPlan_ProducesZip()
    {
        var f   = WriteFile("plugin.dll", "fake dll data");
        var out_ = Path.Combine(TempDir, "out.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "Test",
            Files = [new OivPackageFile(f, "plugins/lspdfr/plugin.dll", 13)]
        };

        var result = await new OivPackageBuilder().BuildAsync(plan, out_);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(out_));
    }

    [Fact]
    public async Task Builder_ValidPlan_ZipContainsAssemblyXml()
    {
        var f    = WriteFile("plugin.dll", "data");
        var out_ = Path.Combine(TempDir, "out2.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "T",
            Files = [new OivPackageFile(f, "plugin.dll", 4)]
        };

        await new OivPackageBuilder().BuildAsync(plan, out_);

        using var zip = ZipFile.OpenRead(out_);
        Assert.Contains(zip.Entries, e => e.Name == "assembly.xml");
    }

    [Fact]
    public async Task Builder_ValidPlan_ZipContainsContentFile()
    {
        var f    = WriteFile("plugin.dll", "data");
        var out_ = Path.Combine(TempDir, "out3.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "T",
            Files = [new OivPackageFile(f, "plugins/lspdfr/plugin.dll", 4)]
        };

        await new OivPackageBuilder().BuildAsync(plan, out_);

        using var zip = ZipFile.OpenRead(out_);
        Assert.Contains(zip.Entries, e => e.FullName == "content/plugins/lspdfr/plugin.dll");
    }

    [Fact]
    public async Task Builder_ValidPlan_ReturnsCorrectFileCount()
    {
        var f1   = WriteFile("a.dll", "d");
        var f2   = WriteFile("b.ini", "d");
        var out_ = Path.Combine(TempDir, "out4.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "T",
            Files =
            [
                new OivPackageFile(f1, "a.dll", 1),
                new OivPackageFile(f2, "b.ini", 1)
            ]
        };

        var result = await new OivPackageBuilder().BuildAsync(plan, out_);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesWritten);
    }

    [Fact]
    public async Task Builder_InvalidPlan_ReturnsFalse()
    {
        var plan  = new OivPackagePlan { Name = "", Files = [], Errors = ["Name required"] };
        var out_  = Path.Combine(TempDir, "invalid.oiv");

        var result = await new OivPackageBuilder().BuildAsync(plan, out_);

        Assert.False(result.Success);
        Assert.False(File.Exists(out_));
    }

    [Fact]
    public async Task Builder_AssemblyXmlContainsTargetFive()
    {
        var f    = WriteFile("x.dll", "d");
        var out_ = Path.Combine(TempDir, "out5.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "T",
            Files = [new OivPackageFile(f, "x.dll", 1)]
        };

        await new OivPackageBuilder().BuildAsync(plan, out_);

        using var zip = ZipFile.OpenRead(out_);
        var asm = zip.GetEntry("assembly.xml")!;
        using var reader = new StreamReader(asm.Open());
        var xml = reader.ReadToEnd();

        Assert.Contains("target=\"Five\"", xml);
    }

    // ── Validator: install path security (sec_001) ───────────────────────────

    [Theory]
    [InlineData("..")]
    [InlineData("../etc/passwd")]
    [InlineData("plugins\\..\\evil")]
    public void Validator_TraversalInstallPath_AddsError(string installPath)
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, installPath, 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("traversal") || e.Contains("path"));
    }

    [Theory]
    [InlineData("/plugins/lspdfr/mod.dll")]
    [InlineData("C:\\plugins\\mod.dll")]
    [InlineData("//server/share/mod.dll")]
    public void Validator_RootedInstallPath_AddsError(string installPath)
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, installPath, 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_RelativeForwardSlashPath_IsValid()
    {
        var f    = WriteFile("mod.dll");
        var plan = new OivPackagePlan
        {
            Name  = "Mod",
            Files = [new OivPackageFile(f, "plugins/lspdfr/mod.dll", 4)]
        };

        var result = new OivPackageValidator().Validate(plan);

        Assert.True(result.IsValid);
    }

    // ── OivAssemblyXmlWriter: version parsing (qual_002) ──────────────────────

    [Theory]
    [InlineData("1.0",   "1", "0")]
    [InlineData("2.3",   "2", "3")]
    [InlineData("v1.2",  "1", "2")]
    [InlineData("1",     "1", "0")]
    [InlineData("1.2.3", "1", "2")]
    [InlineData("abc",   "1", "0")]
    public void XmlWriter_VersionParsing(string version, string major, string minor)
    {
        var f    = WriteFile("x.dll");
        var plan = new OivPackagePlan
        {
            Name    = "M",
            Version = version,
            Files   = [new OivPackageFile(f, "x.dll", 4)]
        };

        var xml = new OivAssemblyXmlWriter().Write(plan);

        Assert.Contains($"<major>{major}</major>", xml);
        Assert.Contains($"<minor>{minor}</minor>", xml);
    }

    // ── OivPackageBuilder: temp-then-move (qual_001) ──────────────────────────

    [Fact]
    public async Task Builder_NoPartialFileOnFailure()
    {
        // Plan references a file that will vanish before the builder reads it.
        var f    = WriteFile("gone.dll", "data");
        var out_ = Path.Combine(TempDir, "partial.oiv");
        var plan = new OivPackagePlan
        {
            Name  = "T",
            Files = [new OivPackageFile(f, "gone.dll", 4)]
        };

        File.Delete(f); // remove source so builder fails mid-copy

        var result = await new OivPackageBuilder().BuildAsync(plan, out_);

        Assert.False(result.Success);
        Assert.False(File.Exists(out_), "Partial .oiv must not remain on disk after failure");
    }

    // ── End-to-end pipeline ───────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_E2E_ScanValidateBuild()
    {
        var srcDir = Path.Combine(TempDir, "e2e_src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "MyPlugin.dll"), "plugin data");
        File.WriteAllText(Path.Combine(srcDir, "MyPlugin.ini"), "[Config]\nkey=value");

        var out_ = Path.Combine(TempDir, "e2e_out.oiv");

        var template = new OivPackagePlan
        {
            Name    = "My E2E Plugin",
            Version = "1.2",
            Author  = "E2E Tester"
        };

        var scanned   = new OivSourceScanner().Scan([srcDir], template);
        var validated = new OivPackageValidator().Validate(scanned);
        var result    = await new OivPackageBuilder().BuildAsync(validated, out_);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.FilesWritten);
        Assert.True(File.Exists(out_));

        using var zip = ZipFile.OpenRead(out_);
        Assert.Contains(zip.Entries, e => e.Name == "assembly.xml");
        Assert.Equal(3, zip.Entries.Count); // assembly.xml + 2 content files
    }

    [Fact]
    public async Task Pipeline_E2E_BlocksExeInSourceFolder()
    {
        var srcDir = Path.Combine(TempDir, "e2e_exe");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "MyPlugin.dll"), "dll data");
        File.WriteAllText(Path.Combine(srcDir, "setup.exe"), "exe data");

        var template = new OivPackagePlan { Name = "Mod" };
        var scanned  = new OivSourceScanner().Scan([srcDir], template);

        Assert.Single(scanned.Files); // exe filtered out
        Assert.Contains(scanned.Warnings, w => w.Contains(".exe"));
    }
}
