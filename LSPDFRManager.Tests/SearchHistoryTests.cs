using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for search history trimming in <see cref="LibraryViewModel"/>.
/// RecordSearchHistory is private; it is exercised through the public
/// <see cref="LibraryViewModel.SearchQuery"/> property.
/// </summary>
[Collection("AppData serial")]
public class SearchHistoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"lspm_sh_{Guid.NewGuid():N}");
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public SearchHistoryTests()
    {
        Directory.CreateDirectory(_tempRoot);
        AppDataPaths.OverrideRoot(Path.Combine(_tempRoot, "AppData"));
        AppDataPaths.EnsureRootExists();
        File.WriteAllText(AppDataPaths.LibraryFile, "[]");
        _library.Mods.Clear();
        AppConfig.Instance.LibrarySearchHistory.Clear();
    }

    public void Dispose()
    {
        try { _library.Mods.Clear(); } catch { }
        try { AppConfig.Instance.LibrarySearchHistory.Clear(); } catch { }
        AppDataPaths.ClearOverride();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void SearchQuery_Set11Times_HistoryNeverExceeds10Entries()
    {
        var vm = new LibraryViewModel();

        for (var i = 1; i <= 11; i++)
            vm.SearchQuery = $"term{i:D2}";   // each value is ≥ 2 chars so it qualifies

        Assert.True(
            AppConfig.Instance.LibrarySearchHistory.Count <= 10,
            $"Expected ≤ 10 entries but got {AppConfig.Instance.LibrarySearchHistory.Count}");
    }

    [Fact]
    public void SearchQuery_Set11Times_OldestTermEvicted()
    {
        var vm = new LibraryViewModel();

        // "term01" is the first/oldest unique term
        for (var i = 1; i <= 11; i++)
            vm.SearchQuery = $"term{i:D2}";

        // After 11 inserts the list is capped at 10; newest is at index 0.
        // "term01" must have been evicted.
        Assert.DoesNotContain(
            "term01",
            AppConfig.Instance.LibrarySearchHistory,
            StringComparer.OrdinalIgnoreCase);
    }
}
