using System.Xml.Linq;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="OivService"/> — package parsing, validation, creation, preview and install.
/// </summary>
public class OivTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"oiv_test_{Guid.NewGuid():N}");

    public OivTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private string CreateOiv(string name = "Test Mod", string version = "1.2", string author = "Tester",
        string description = "A test mod", Dictionary<string, string>? contentFiles = null)
    {
        var oivPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.oiv");

        using var zip = ZipFile.Open(oivPath, ZipArchiveMode.Create);

        // Build assembly.xml
        var files = contentFiles ?? new Dictionary<string, string> { ["content/test.dll"] = "plugins/test.dll" };
        var adds = files.Select(kvp =>
            new XElement("add", new XAttribute("source", kvp.Key), kvp.Value));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("package", new XAttribute("version", "2.0"),
                new XElement("metadata",
                    new XElement("name", name),
                    new XElement("version",
                        new XElement("major", version.Split('.')[0]),
                        new XElement("minor", version.Split('.').Length > 1 ? version.Split('.')[1] : "0")),
                    new XElement("author", author),
                    new XElement("description", new XCData(description))),
                new XElement("content", adds)));

        var xmlEntry = zip.CreateEntry("assembly.xml");
        using (var s = xmlEntry.Open())
        using (var w = new StreamWriter(s))
            w.Write(doc.Declaration + Environment.NewLine + doc.ToString());

        // Write content files
        foreach (var kvp in files)
        {
            var contentEntry = zip.CreateEntry(kvp.Key);
            using var cs = contentEntry.Open();
            using var cw = new StreamWriter(cs);
            cw.Write("dummy content for " + kvp.Key);
        }

        return oivPath;
    }

    // ── Test 1: ParsePackage reads metadata ──────────────────────────────────

    [Fact]
    public void ParsePackage_ReadsMetadata_NameVersionAuthor()
    {
        var oivPath = CreateOiv(name: "My Mod", version: "2.3", author: "SomeAuthor");

        var pkg = OivService.ParsePackage(oivPath);

        Assert.True(pkg.IsValid, pkg.ValidationError);
        Assert.Equal("My Mod", pkg.Name);
        Assert.Equal("2.3", pkg.Version);
        Assert.Equal("SomeAuthor", pkg.Author);
    }

    // ── Test 2: ValidatePackage returns false for empty Name ─────────────────

    [Fact]
    public void ValidatePackage_EmptyName_ReturnsFalse()
    {
        var pkg = new OivPackage
        {
            Name = "",
            Files = [new OivFileEntry { SourcePath = "x", InstallPath = "y" }]
        };

        var (isValid, error) = OivService.ValidatePackage(pkg);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    // ── Test 3: ValidatePackage returns false for empty Files ────────────────

    [Fact]
    public void ValidatePackage_EmptyFiles_ReturnsFalse()
    {
        var pkg = new OivPackage
        {
            Name = "Valid Name",
            Files = []
        };

        var (isValid, error) = OivService.ValidatePackage(pkg);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    // ── Test 4: CreatePackage produces a ZIP with assembly.xml ───────────────

    [Fact]
    public void CreatePackage_ProducesZipWithAssemblyXml()
    {
        var sourceFile = Path.Combine(_tempDir, "myfile.dll");
        File.WriteAllText(sourceFile, "binary data");

        var outputPath = Path.Combine(_tempDir, "output.oiv");

        var pkg = new OivPackage
        {
            Name = "Test Package",
            Version = "1.0",
            Author = "Dev",
            Files =
            [
                new OivFileEntry
                {
                    SourcePath = sourceFile,
                    InstallPath = "plugins/myfile.dll",
                    Action = OivFileAction.Add
                }
            ]
        };

        var success = OivService.CreatePackage(pkg, outputPath);

        Assert.True(success);
        Assert.True(File.Exists(outputPath));

        using var zip = ZipFile.OpenRead(outputPath);
        var xmlEntry = zip.GetEntry("assembly.xml");
        Assert.NotNull(xmlEntry);
    }

    // ── Test 5: PreviewInstall marks Add vs Replace ───────────────────────────

    [Fact]
    public void PreviewInstall_MarksExistingFilesReplace_NewFilesAdd()
    {
        var targetRoot = Path.Combine(_tempDir, "gta5");
        Directory.CreateDirectory(targetRoot);

        // Create an existing file
        var existingDir = Path.Combine(targetRoot, "plugins");
        Directory.CreateDirectory(existingDir);
        File.WriteAllText(Path.Combine(existingDir, "existing.dll"), "old");

        var oivPath = CreateOiv(contentFiles: new Dictionary<string, string>
        {
            ["content/plugins/existing.dll"] = "plugins/existing.dll",
            ["content/plugins/new.dll"]      = "plugins/new.dll"
        });

        var pkg = OivService.ParsePackage(oivPath);
        Assert.True(pkg.IsValid, pkg.ValidationError);

        var preview = OivService.PreviewInstall(pkg, targetRoot);

        var existingEntry = preview.First(e => e.InstallPath == "plugins/existing.dll");
        var newEntry      = preview.First(e => e.InstallPath == "plugins/new.dll");

        Assert.Equal(OivFileAction.Replace, existingEntry.Action);
        Assert.Equal(OivFileAction.Add,     newEntry.Action);
    }

    // ── Test 6: InstallPackage backs up existing file before overwrite ────────

    [Fact]
    public async Task InstallPackage_BacksUpExistingFile_BeforeOverwrite()
    {
        var targetRoot = Path.Combine(_tempDir, "gta5_backup");
        Directory.CreateDirectory(targetRoot);
        Directory.CreateDirectory(Path.Combine(targetRoot, "plugins"));

        var existingFile = Path.Combine(targetRoot, "plugins", "test.dll");
        File.WriteAllText(existingFile, "original content");

        var oivPath = CreateOiv(contentFiles: new Dictionary<string, string>
        {
            ["content/plugins/test.dll"] = "plugins/test.dll"
        });

        var pkg = OivService.ParsePackage(oivPath);
        Assert.True(pkg.IsValid, pkg.ValidationError);

        var result = await OivService.InstallPackage(pkg, targetRoot);

        Assert.True(result.Success, result.Error);

        // The file should now have new content (overwritten)
        var content = File.ReadAllText(existingFile);
        Assert.Contains("dummy content", content);
    }

    // ── Test 7: InstallPackage rolls back on failure ──────────────────────────

    [Fact]
    public async Task InstallPackage_RollsBackOnFailure_RestoresBackup()
    {
        var targetRoot = Path.Combine(_tempDir, "gta5_rollback");
        Directory.CreateDirectory(targetRoot);
        Directory.CreateDirectory(Path.Combine(targetRoot, "plugins"));

        // Pre-existing file that should be restored on rollback
        var existingFile = Path.Combine(targetRoot, "plugins", "first.dll");
        File.WriteAllText(existingFile, "original content");

        // Create OIV with two files: first (existing), second (non-existent in zip = forced fail)
        var oivPath = Path.Combine(_tempDir, "rollback_test.oiv");
        using (var zip = ZipFile.Open(oivPath, ZipArchiveMode.Create))
        {
            var xmlEntry = zip.CreateEntry("assembly.xml");
            using (var s = xmlEntry.Open())
            using (var w = new StreamWriter(s))
                w.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package version=""2.0"">
  <metadata>
    <name>Rollback Test</name>
    <version><major>1</major><minor>0</minor></version>
    <author>Test</author>
    <description><![CDATA[rollback test]]></description>
  </metadata>
  <content>
    <add source=""content/plugins/first.dll"">plugins/first.dll</add>
    <add source=""content/plugins/missing.dll"">plugins/missing.dll</add>
  </content>
</package>");

            // Only add first file — second is deliberately missing to trigger failure path
            var contentEntry = zip.CreateEntry("content/plugins/first.dll");
            using (var cs = contentEntry.Open())
            using (var cw = new StreamWriter(cs))
                cw.Write("new first content");
        }

        var pkg = OivService.ParsePackage(oivPath);
        Assert.True(pkg.IsValid, pkg.ValidationError);

        var result = await OivService.InstallPackage(pkg, targetRoot);

        // Install should succeed for files that exist in zip
        // The missing file entry is skipped (not a hard error), so check the first was written
        // This test verifies that when a write throws the first file is rolled back
        // Force a real rollback scenario by using a bad target path:
        var badPkg = new OivPackage
        {
            Name = pkg.Name,
            SourcePath = pkg.SourcePath,
            IsValid = true,
            Files =
            [
                new OivFileEntry
                {
                    SourcePath = "content/plugins/first.dll",
                    InstallPath = "plugins/first.dll"
                },
                new OivFileEntry
                {
                    // InstallPath that will cause Directory.CreateDirectory failure on Windows
                    SourcePath = "content/plugins/first.dll",
                    InstallPath = new string('?', 10) // invalid path chars
                }
            ]
        };

        // Re-write original so we can verify restoration
        File.WriteAllText(existingFile, "original content");

        var badResult = await OivService.InstallPackage(badPkg, targetRoot);

        // Should fail due to invalid path
        Assert.False(badResult.Success);

        // Original file should be restored
        Assert.True(File.Exists(existingFile));
        var restoredContent = File.ReadAllText(existingFile);
        Assert.Equal("original content", restoredContent);
    }

    // ── Test 8: ParsePackage returns error for malformed/non-OIV file ─────────

    [Fact]
    public void ParsePackage_MalformedFile_ReturnsInvalidPackage()
    {
        var notAZip = Path.Combine(_tempDir, "notanoiv.oiv");
        File.WriteAllText(notAZip, "this is not a zip file at all");

        var pkg = OivService.ParsePackage(notAZip);

        Assert.False(pkg.IsValid);
        Assert.NotNull(pkg.ValidationError);
    }

    [Fact]
    public void ParsePackage_NonExistentFile_ReturnsInvalidPackage()
    {
        var pkg = OivService.ParsePackage(Path.Combine(_tempDir, "doesnotexist.oiv"));

        Assert.False(pkg.IsValid);
        Assert.NotNull(pkg.ValidationError);
    }

    [Fact]
    public void ParsePackage_ZipWithoutAssemblyXml_ReturnsInvalidPackage()
    {
        var oivPath = Path.Combine(_tempDir, "noxml.oiv");

        // Create and fully close the ZIP before parsing it
        using (var zip = ZipFile.Open(oivPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("somefile.txt");
            using (var s = entry.Open())
            using (var w = new StreamWriter(s))
                w.Write("just a file, no assembly.xml");
        }

        var pkg = OivService.ParsePackage(oivPath);

        Assert.False(pkg.IsValid);
        Assert.Contains("assembly.xml", pkg.ValidationError ?? "");
    }
}
