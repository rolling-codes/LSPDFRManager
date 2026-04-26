using System.IO.Compression;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Integration tests using real archives + full FileInstaller pipeline.
/// Tests real-world scenarios: corruption, locks, traversal, etc.
/// </summary>
public class InstallIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"integration_{Guid.NewGuid():N}");
    private readonly string _tempSource = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");

    public InstallIntegrationTests()
    {
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_tempSource);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
            if (Directory.Exists(_tempSource))
                Directory.Delete(_tempSource, recursive: true);
        }
        catch { }
    }

    // ── Scenario 1: Path Traversal (Real ZIP) ──────────────────────────

    [Fact]
    public async Task Scenario1_PathTraversal_BlocksAndRollsBack()
    {
        var zipPath = Path.Combine(_tempSource, "traversal.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntry("safe.dll").Open().Close();
            zip.CreateEntry("../../escape.exe").Open().Close();
        }

        var mod = new ModInfo { Name = "Traversal Test", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("traversal", result.Error.ToLower());
        Assert.False(File.Exists(Path.Combine(_tempRoot, "safe.dll")), "Rollback should delete even safe files if one fails");
    }

    // ── Scenario 2: Encryption (Not Supported) ────────────────────────

    [Fact]
    public async Task Scenario2_EncryptedZip_FailsGracefully()
    {
        var zipPath = Path.Combine(_tempSource, "encrypted.zip");
        File.WriteAllText(zipPath, "PK\x03\x04 corrupt data...");

        var mod = new ModInfo { Name = "Corrupt Test", SourcePath = zipPath };
        var result = await FileInstaller.InstallAsync(mod, _tempRoot);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    // ── Queue Integration ──────────────────────────────────────────────

    [Fact]
    public async Task QueueIntegration_FailureEventFires()
    {
        var queue = InstallQueue.Instance;
        var tcs = new TaskCompletionSource<bool>();
        string? failureError = null;

        Action<ModInfo, InstallResult> handler = (mod, result) =>
        {
            if (mod.Name == "Queue Test")
            {
                failureError = result.Error;
                tcs.TrySetResult(true);
            }
        };

        queue.InstallFailedWithResult += handler;

        try
        {
            var zipPath = Path.Combine(_tempSource, "queue_fail.zip");
            File.WriteAllText(zipPath, "not a zip");

            var mod = new ModInfo { Name = "Queue Test", SourcePath = zipPath };
            queue.Enqueue(mod);

            // Wait for event with timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;

            Assert.True(completed, "InstallFailedWithResult event should fire");
            Assert.NotNull(failureError);
        }
        finally
        {
            queue.InstallFailedWithResult -= handler;
        }
    }
}
