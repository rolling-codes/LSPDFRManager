using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class PersistenceAndConflictTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspdfr_persist_{Guid.NewGuid():N}");

    public PersistenceAndConflictTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void JsonFileStore_Save_ReplacesExistingFileAtomically()
    {
        var path = Path.Combine(_tempRoot, "settings.json");
        var store = new JsonFileStore<TestSettings>(path);

        store.Save(new TestSettings { Name = "first", Count = 1 });
        store.Save(new TestSettings { Name = "second", Count = 2 });

        var loaded = store.LoadOrDefault(() => new TestSettings());

        Assert.Equal("second", loaded.Name);
        Assert.Equal(2, loaded.Count);
        Assert.Empty(Directory.GetFiles(_tempRoot, "*.tmp"));
    }

    [Fact]
    public void FindConflicts_DetectsDisabledPhysicalFile()
    {
        var pluginPath = Path.Combine(_tempRoot, "plugins", "Callouts.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath + ".disabled", "disabled");

        var service = new InstalledModFileService();
        var candidate = new InstalledMod
        {
            Name = "Candidate",
            InstalledFiles = [pluginPath]
        };

        var conflicts = service.FindConflicts([], candidate);

        Assert.Contains(conflicts, conflict => conflict.Contains("Disabled file conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Uninstall_DeletesActiveAndDisabledDetectedFiles()
    {
        var pluginPath = Path.Combine(_tempRoot, "plugins", "Callouts.dll");
        var disabledPath = pluginPath + ".disabled";
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath, "active");
        File.WriteAllText(disabledPath, "disabled");

        var service = new InstalledModFileService();
        var mod = new InstalledMod
        {
            Name = "Callouts",
            IsEnabled = false,
            InstallPath = _tempRoot,
            InstalledFiles = [pluginPath]
        };

        var result = service.Uninstall(mod);

        Assert.True(result.Success, result.StatusMessage);
        Assert.False(File.Exists(pluginPath));
        Assert.False(File.Exists(disabledPath));
        Assert.Contains(pluginPath, result.DeletedFiles);
        Assert.Contains(disabledPath, result.DeletedFiles);
    }

    [Fact]
    public void Uninstall_SkipsSharedFilesAndReportsThemWithoutFailing()
    {
        var sharedPath = Path.Combine(_tempRoot, "plugins", "shared.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(sharedPath)!);
        File.WriteAllText(sharedPath, "shared");

        var target = new InstalledMod
        {
            Id = Guid.NewGuid(),
            Name = "Target",
            InstallPath = _tempRoot,
            InstalledFiles = [sharedPath]
        };
        var other = new InstalledMod
        {
            Id = Guid.NewGuid(),
            Name = "Other",
            InstallPath = _tempRoot,
            InstalledFiles = [sharedPath]
        };

        var service = new InstalledModFileService();
        var result = service.Uninstall(target, [target, other]);

        Assert.True(result.Success, result.StatusMessage);
        Assert.True(File.Exists(sharedPath));
        Assert.Contains(sharedPath, result.SkippedSharedFiles);
        Assert.Empty(result.FailedFiles);
    }

    private sealed class TestSettings
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
