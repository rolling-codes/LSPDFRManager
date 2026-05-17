using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class DllDuplicateScannerTests : IDisposable
{
    private readonly string _tempGta;
    private readonly AppConfig _savedConfig;

    public DllDuplicateScannerTests()
    {
        _tempGta = Path.Combine(Path.GetTempPath(), $"dds_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempGta);
        Directory.CreateDirectory(Path.Combine(_tempGta, "plugins"));
        Directory.CreateDirectory(Path.Combine(_tempGta, "plugins", "LSPDFR"));

        _savedConfig = AppConfig.Instance;
        AppConfig.Instance.GtaPath = _tempGta;
    }

    public void Dispose()
    {
        AppConfig.Instance.GtaPath = _savedConfig.GtaPath;
        if (Directory.Exists(_tempGta))
            Directory.Delete(_tempGta, recursive: true);
    }

    private void PlaceDll(string relPath)
    {
        var full = Path.Combine(_tempGta, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, []);
    }

    [Fact]
    public void NoDuplicates_ReturnsEmpty()
    {
        PlaceDll("plugins/RAGENativeUI.dll");
        var results = new DllDuplicateScanner().Scan();
        Assert.Empty(results);
    }

    [Fact]
    public void Duplicate_Detected()
    {
        PlaceDll("plugins/RAGENativeUI.dll");
        PlaceDll("plugins/LSPDFR/RAGENativeUI.dll");
        var results = new DllDuplicateScanner().Scan();
        Assert.Contains(results, r => r.DllName.Equals("RAGENativeUI.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Duplicate_ReportsAllCopies()
    {
        PlaceDll("plugins/RAGENativeUI.dll");
        PlaceDll("plugins/LSPDFR/RAGENativeUI.dll");
        var result = new DllDuplicateScanner().Scan()
            .First(r => r.DllName.Equals("RAGENativeUI.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void KnownSharedDep_FlaggedCorrectly()
    {
        PlaceDll("plugins/RAGENativeUI.dll");
        PlaceDll("plugins/LSPDFR/RAGENativeUI.dll");
        var result = new DllDuplicateScanner().Scan()
            .First(r => r.DllName.Equals("RAGENativeUI.dll", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.IsKnownSharedDep);
    }

    [Fact]
    public void UnknownDuplicate_IsNotFlaggedAsKnownSharedDep()
    {
        PlaceDll("plugins/MyCustomLib.dll");
        PlaceDll("plugins/LSPDFR/MyCustomLib.dll");
        var result = new DllDuplicateScanner().Scan()
            .First(r => r.DllName.Equals("MyCustomLib.dll", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.IsKnownSharedDep);
    }

    [Fact]
    public void EmptyGtaPath_ReturnsEmpty()
    {
        AppConfig.Instance.GtaPath = "";
        var results = new DllDuplicateScanner().Scan();
        Assert.Empty(results);
    }

    [Fact]
    public void NonexistentGtaPath_ReturnsEmpty()
    {
        AppConfig.Instance.GtaPath = Path.Combine(Path.GetTempPath(), "nonexistent_path_xyz");
        var results = new DllDuplicateScanner().Scan();
        Assert.Empty(results);
    }
}
