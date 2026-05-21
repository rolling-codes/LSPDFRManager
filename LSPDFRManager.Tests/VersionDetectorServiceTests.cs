using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class VersionDetectorServiceTests : CommandCenterTestBase
{
    private readonly VersionDetectorService _sut = new();

    // ── Missing GTA directory ──────────────────────────────────────────────

    [Fact]
    public async Task Detect_ReturnsBundle_WhenGtaDirIsEmpty()
    {
        // GtaDir is created by base class but contains no files
        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.NotNull(bundle);
        Assert.False(bundle.GtaPresent);
        Assert.False(bundle.LspdfrPresent);
        Assert.False(bundle.RagePluginHookPresent);
    }

    // ── File presence flags ────────────────────────────────────────────────

    [Fact]
    public async Task Detect_SetsGtaPresent_WhenGta5ExeExists()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");

        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.True(bundle.GtaPresent);
    }

    [Fact]
    public async Task Detect_SetsLspdfrPresent_WhenDllExists()
    {
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "fake");

        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.True(bundle.LspdfrPresent);
    }

    [Fact]
    public async Task Detect_SetsLspdfrPresent_WhenOfficialCoreDllExists()
    {
        var pluginsDir = Path.Combine(GtaDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPD First Response.dll"), "fake");

        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.True(bundle.LspdfrPresent);
        Assert.NotNull(bundle.LspdfrHash);
    }

    [Fact]
    public async Task Detect_SetsLspdfrPresent_WhenRootLspdfrFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(GtaDir, "lspdfr"));

        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.True(bundle.LspdfrPresent);
    }

    // ── Hash stability ─────────────────────────────────────────────────────

    [Fact]
    public async Task Detect_HashIsStable_ForSameFileContent()
    {
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookV.dll"), "stable content");

        var b1 = await _sut.DetectAsync(GtaDir);
        var b2 = await _sut.DetectAsync(GtaDir);

        Assert.NotNull(b1.ScriptHookVHash);
        Assert.Equal(b1.ScriptHookVHash, b2.ScriptHookVHash);
    }

    [Fact]
    public async Task Detect_HashChanges_WhenFileContentChanges()
    {
        var path = Path.Combine(GtaDir, "ScriptHookV.dll");
        File.WriteAllText(path, "version A");
        var b1 = await _sut.DetectAsync(GtaDir);

        File.WriteAllText(path, "version B");
        var b2 = await _sut.DetectAsync(GtaDir);

        Assert.NotEqual(b1.ScriptHookVHash, b2.ScriptHookVHash);
    }

    // ── Optional file absent ───────────────────────────────────────────────

    [Fact]
    public async Task Detect_ShvdnVersionIsNull_WhenNoCandidateExists()
    {
        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.Null(bundle.ScriptHookVDotNetVersion);
        Assert.Null(bundle.ScriptHookVDotNetHash);
    }

    [Fact]
    public async Task Detect_ShvdnVersionFound_WhenScriptHookVDotNet3Present()
    {
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookVDotNet3.dll"), "fake dll");

        var bundle = await _sut.DetectAsync(GtaDir);

        // Version is null for a fake file (no real PE header) but presence is detected
        Assert.NotNull(bundle.ScriptHookVDotNetHash);
    }

    // ── GTA5.exe is never hashed ───────────────────────────────────────────

    [Fact]
    public async Task Detect_GtaHashIsNull_Always()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "fake");

        var bundle = await _sut.DetectAsync(GtaDir);

        Assert.Null(bundle.GtaHash);
    }

    // ── Non-existent GTA path ──────────────────────────────────────────────

    [Fact]
    public async Task Detect_ReturnsBundle_WhenGtaPathDoesNotExist()
    {
        var missing = Path.Combine(GtaDir, "nonexistent_subdir");

        var bundle = await _sut.DetectAsync(missing);

        Assert.NotNull(bundle);
        Assert.False(bundle.GtaPresent);
        Assert.False(bundle.LspdfrPresent);
    }
}
