using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class ConfigDiscoveryTests : IDisposable
{
    private readonly string _root;

    public ConfigDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lspdfrtest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string MakeFile(string relativePath)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "[General]\nKey=Value\n");
        return full;
    }

    [Fact]
    public void ConfigDiscovery_FindsIniUnderPluginsLspdfr()
    {
        MakeFile("plugins/lspdfr/LSPDFR.ini");
        var svc = new ConfigDiscoveryService(_root);
        var results = svc.DiscoverAll();
        Assert.Contains(results, r => r.RelativePath.Equals("plugins/lspdfr/LSPDFR.ini", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfigDiscovery_FindsXmlUnderEls()
    {
        MakeFile("ELS/vehicles/police.xml");
        var svc = new ConfigDiscoveryService(_root);
        var results = svc.DiscoverAll();
        Assert.Contains(results, r => r.FileType == "xml" && r.RelativePath.Contains("police.xml"));
    }

    [Fact]
    public void ConfigDiscovery_EmptyPath_ReturnsEmpty_DoesNotThrow()
    {
        var svc = new ConfigDiscoveryService("");
        var results = svc.DiscoverAll();
        Assert.Empty(results);
    }

    [Fact]
    public void ConfigDiscovery_InfersPluginOwner()
    {
        MakeFile("plugins/lspdfr/UltimateBackup/UltimateBackup.ini");
        var svc = new ConfigDiscoveryService(_root);
        var results = svc.DiscoverAll();
        var found = results.FirstOrDefault(r => r.RelativePath.Contains("UltimateBackup.ini"));
        Assert.NotNull(found);
        Assert.Equal("UltimateBackup", found.PluginOwner);
    }
}

public class IniParserTests : IDisposable
{
    private readonly string _dir;

    public IniParserTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "iniparser_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string WriteIni(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void IniParser_ParsesSimpleIni()
    {
        var path = WriteIni("test.ini", "[Settings]\nFoo=Bar\nBaz = Qux\n");
        var result = IniParser.Parse(path);
        Assert.True(result.ContainsKey("Settings"));
        Assert.Equal("Bar", result["Settings"]["Foo"]);
        Assert.Equal("Qux", result["Settings"]["Baz"]);
    }

    [Fact]
    public void IniParser_PreviewPatch_ReturnsCorrectOldAndNew()
    {
        var path = WriteIni("keys.ini", "[Keys]\nBackupMenu=F5\n");
        var rules = new[]
        {
            new PresetPatchRule { File = "keys.ini", MatchKeys = ["BackupMenu"], SetValue = "None", Reason = "Test" }
        };
        var previews = IniParser.PreviewPatch(path, rules);
        Assert.Single(previews);
        Assert.Equal("F5", previews[0].OldValue);
        Assert.Equal("None", previews[0].NewValue);
        Assert.True(previews[0].WouldChange);
    }

    [Fact]
    public void IniParser_Apply_WritesOnlyChangedLine()
    {
        var path = WriteIni("apply.ini", "[Keys]\nBackupMenu=F5\nOtherKey=G\n");
        var rules = new[]
        {
            new PresetPatchRule { File = "apply.ini", MatchKeys = ["BackupMenu"], SetValue = "None", Reason = "Test" }
        };
        var ok = IniParser.Apply(path, rules, backupFirst: false);
        Assert.True(ok);
        var content = File.ReadAllText(path);
        Assert.Contains("BackupMenu=None", content);
        Assert.Contains("OtherKey=G", content);
    }

    [Fact]
    public void IniParser_Apply_CreatesBackupFile()
    {
        var path = WriteIni("backup.ini", "[Keys]\nMenuKey=F6\n");
        var rules = new[]
        {
            new PresetPatchRule { File = "backup.ini", MatchKeys = ["MenuKey"], SetValue = "RightThumb", Reason = "Test" }
        };
        IniParser.Apply(path, rules, backupFirst: true);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void IniParser_InvalidFile_DoesNotThrow()
    {
        var result = IniParser.Parse(Path.Combine(_dir, "nonexistent.ini"));
        Assert.Empty(result);
    }

    [Fact]
    public void IniParser_Apply_SecondApply_DoesNotOverwriteOriginalBak()
    {
        var path = WriteIni("double.ini", "[Keys]\nMenuKey=F5\n");
        var rules = new[]
        {
            new PresetPatchRule { File = "double.ini", MatchKeys = ["MenuKey"], SetValue = "None", Reason = "Test" }
        };
        IniParser.Apply(path, rules, backupFirst: true);
        var bakContentAfterFirst = File.ReadAllText(path + ".bak");

        IniParser.Apply(path, rules, backupFirst: true);
        var bakContentAfterSecond = File.ReadAllText(path + ".bak");

        // .bak should still hold the original content, not the already-patched version
        Assert.Equal(bakContentAfterFirst, bakContentAfterSecond);
        Assert.Contains("MenuKey=F5", bakContentAfterFirst);
    }

    [Fact]
    public void IniParser_Apply_MissingFile_ReturnsFalse_DoesNotThrow()
    {
        var rules = new[]
        {
            new PresetPatchRule { File = "missing.ini", MatchKeys = ["Key"], SetValue = "X", Reason = "Test" }
        };
        var ok = IniParser.Apply(Path.Combine(_dir, "missing.ini"), rules, backupFirst: false);
        Assert.False(ok);
    }

    [Fact]
    public void IniParser_Parse_BinaryContent_DoesNotThrow()
    {
        var path = Path.Combine(_dir, "binary.ini");
        File.WriteAllBytes(path, [0x00, 0xFF, 0xFE, 0x0D, 0x0A, 0x41, 0x42]);
        var result = IniParser.Parse(path);
        Assert.NotNull(result);
    }
}

public class KeybindScannerTests : IDisposable
{
    private readonly string _dir;

    public KeybindScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "keybind_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private DiscoveredConfig WriteConfig(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return new DiscoveredConfig
        {
            AbsolutePath = path,
            RelativePath = name,
            FileType = "ini",
            PluginOwner = null,
        };
    }

    [Fact]
    public void KeybindScanner_DetectsDuplicateKey()
    {
        var a = WriteConfig("a.ini", "[Keys]\nMenuKey=F5\n");
        var b = WriteConfig("b.ini", "[Keys]\nMenuKey=F5\n");
        var scanner = new KeybindConflictScanner();
        var conflicts = scanner.Scan([a, b]);
        Assert.Contains(conflicts, c => c.KeyValue.Equals("F5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KeybindScanner_IgnoresNoneAndNumbers()
    {
        var a = WriteConfig("a.ini", "[Keys]\nKey1=None\nKey2=0\nKey3=42\n");
        var b = WriteConfig("b.ini", "[Keys]\nKey1=None\nKey2=0\nKey3=42\n");
        var scanner = new KeybindConflictScanner();
        var conflicts = scanner.Scan([a, b]);
        Assert.Empty(conflicts);
    }

    [Fact]
    public void KeybindScanner_NoDuplicates_ReturnsEmpty()
    {
        var a = WriteConfig("a.ini", "[Keys]\nMenuKey=F5\n");
        var b = WriteConfig("b.ini", "[Keys]\nMenuKey=F6\n");
        var scanner = new KeybindConflictScanner();
        var conflicts = scanner.Scan([a, b]);
        Assert.Empty(conflicts);
    }
}

public class PresetPatchTests : IDisposable
{
    private readonly string _root;

    public PresetPatchTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "presetpatch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string MakeIni(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void PresetPatch_Preview_DoesNotWriteFile()
    {
        var fullPath = MakeIni("plugins/lspdfr/keys.ini", "[Keys]\nBackupMenu=F5\n");
        var originalContent = File.ReadAllText(fullPath);

        var preset = new PatrolSetupPreset
        {
            PresetId = "test",
            DisplayName = "Test",
            Description = "",
            Rules =
            [
                new PresetPatchRule
                {
                    File = "plugins/lspdfr/keys.ini",
                    MatchKeys = ["BackupMenu"],
                    SetValue = "None",
                    Reason = "Test",
                }
            ],
        };

        var svc = new PresetPatchService(_root);
        var previews = svc.Preview(preset);

        Assert.NotEmpty(previews);
        Assert.Equal(originalContent, File.ReadAllText(fullPath));
    }

    [Fact]
    public void PresetPatch_Apply_ChangesValueInFile()
    {
        MakeIni("plugins/lspdfr/keys.ini", "[Keys]\nBackupMenu=F5\n");

        var preset = new PatrolSetupPreset
        {
            PresetId = "test",
            DisplayName = "Test",
            Description = "",
            Rules =
            [
                new PresetPatchRule
                {
                    File = "plugins/lspdfr/keys.ini",
                    MatchKeys = ["BackupMenu"],
                    SetValue = "None",
                    Reason = "Test",
                }
            ],
        };

        var svc = new PresetPatchService(_root);
        var ok = svc.Apply(preset, backupFirst: false);

        Assert.True(ok);
        var content = File.ReadAllText(Path.Combine(_root, "plugins", "lspdfr", "keys.ini"));
        Assert.Contains("BackupMenu=None", content);
    }
}
