using System.IO.Compression;
using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

[Collection("CommandCenter")]
public class GtaFileBackupServiceTests : CommandCenterTestBase
{
    private readonly GtaFileBackupService _service = new();

    private RemovalCandidate MakeCandidate(string rel, string full, bool isDir = false) =>
        new()
        {
            RelativePath = rel,
            FullPath = full,
            Classification = CandidateClassification.LspdfrCore,
            RiskLevel = CleanupRiskLevel.Low,
            Reason = "test",
            IsDirectory = isDir,
        };

    // Test 10
    [Fact]
    public async Task CreateCleanupBackup_PreservesGtaRootRelativePaths()
    {
        var pluginPath = Path.Combine(GtaDir, "plugins", "LSPD First Response.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath, "data");

        var candidates = new List<RemovalCandidate>
        {
            MakeCandidate(@"plugins\LSPD First Response.dll", pluginPath),
        };

        var result = await _service.CreateCleanupBackupAsync(GtaDir, candidates, CleanupMode.SafeCoreReset);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.ZipPath);

        using var zip = ZipFile.OpenRead(result.ZipPath!);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("plugins/LSPD First Response.dll", entryNames);
    }

    // Test 11
    [Fact]
    public async Task CreateCleanupBackup_IncludesManifest()
    {
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "exe");

        var candidates = new List<RemovalCandidate>
        {
            MakeCandidate("RAGEPluginHook.exe", Path.Combine(GtaDir, "RAGEPluginHook.exe")),
        };

        var result = await _service.CreateCleanupBackupAsync(GtaDir, candidates, CleanupMode.SafeCoreReset);

        Assert.True(result.Success, result.ErrorMessage);
        using var zip = ZipFile.OpenRead(result.ZipPath!);
        var manifest = zip.GetEntry("cleanup_manifest.json");
        Assert.NotNull(manifest);

        using var stream = manifest!.Open();
        var doc = await JsonDocument.ParseAsync(stream);
        Assert.True(doc.RootElement.TryGetProperty("GtaRoot", out _));
        Assert.True(doc.RootElement.TryGetProperty("CleanupMode", out _));
    }

    // Test 12
    [Fact]
    public async Task CreateCleanupBackup_OnlyIncludesSelectedCandidates()
    {
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "exe");
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookV.dll"), "dll");

        var candidates = new List<RemovalCandidate>
        {
            MakeCandidate("RAGEPluginHook.exe", Path.Combine(GtaDir, "RAGEPluginHook.exe")),
            // ScriptHookV NOT in candidates
        };

        var result = await _service.CreateCleanupBackupAsync(GtaDir, candidates, CleanupMode.SafeCoreReset);

        Assert.True(result.Success, result.ErrorMessage);
        using var zip = ZipFile.OpenRead(result.ZipPath!);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("RAGEPluginHook.exe", entryNames);
        Assert.DoesNotContain("ScriptHookV.dll", entryNames);
    }
}
