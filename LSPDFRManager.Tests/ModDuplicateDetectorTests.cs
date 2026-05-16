using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="ModDuplicateDetector.FindExactDuplicate"/>.
/// Uses the "AppData serial" collection so tests run sequentially and the
/// ModLibraryService singleton is not raced by parallel tests.
/// </summary>
[Collection("AppData serial")]
public class ModDuplicateDetectorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_dup_{Guid.NewGuid():N}");
    private readonly ModLibraryService _library = ModLibraryService.Instance;
    private readonly ModDuplicateDetector _detector = new();

    public ModDuplicateDetectorTests()
    {
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        _library.Mods.Clear();
    }

    public void Dispose()
    {
        try { _library.Mods.Clear(); } catch { }
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void FindExactDuplicate_EmptyLibrary_ReturnsNull()
    {
        var result = _detector.FindExactDuplicate("ELS");

        Assert.Null(result);
    }

    [Fact]
    public void FindExactDuplicate_ExactNameMatch_ReturnsMod()
    {
        var mod = new InstalledMod { Name = "StopThePed", IsEnabled = true };
        _library.Mods.Add(mod);

        var result = _detector.FindExactDuplicate("StopThePed");

        Assert.NotNull(result);
        Assert.Equal(mod.Id, result.Id);
    }

    [Fact]
    public void FindExactDuplicate_NoMatchingName_ReturnsNull()
    {
        _library.Mods.Add(new InstalledMod { Name = "UltimateBackup", IsEnabled = true });

        var result = _detector.FindExactDuplicate("ELS");

        Assert.Null(result);
    }

    [Fact]
    public void FindExactDuplicate_CaseInsensitiveMatch_ReturnsMod()
    {
        var mod = new InstalledMod { Name = "ELS", IsEnabled = true };
        _library.Mods.Add(mod);

        var result = _detector.FindExactDuplicate("els");

        Assert.NotNull(result);
        Assert.Equal(mod.Id, result.Id);
    }
}
