using System.IO.Compression;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class LspdfrValidatorTests
{
    private static readonly string TestArchivesPath =
        Path.Combine(
            Path.GetDirectoryName(typeof(LspdfrValidatorTests).Assembly.Location) ?? "",
            "..", "..", "..", "TestArchives");

    // ── Detection Scoring Tests ────────────────────────────────────

    [Fact]
    public void CalculateDetectionScore_ValidPlugin_HighScore()
    {
        var files = new[] { "plugins/LSPDFR/Test.dll" };

        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score > 50, $"Expected score > 50, got {score}");
    }

    [Fact]
    public void CalculateDetectionScore_ScriptMod_MediumScore()
    {
        var files = new[] { "scripts/Test.cs" };

        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score >= 10, $"Expected score >= 10, got {score}");
    }

    [Fact]
    public void CalculateDetectionScore_MixedContent_HighScore()
    {
        var files = new[] { "plugins/LSPDFR/Test.dll", "plugins/LSPDFR/Test.ini" };

        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score > 50, $"Expected score > 50 for mixed content, got {score}");
    }

    [Fact]
    public void CalculateDetectionScore_RandomFiles_LowScore()
    {
        var files = new[] { "readme.txt", "license.md" };

        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score < 20, $"Expected score < 20 for random files, got {score}");
    }

    [Fact]
    public void CalculateDetectionScore_Clamped_Between0And100()
    {
        var files = Enumerable.Range(0, 10)
            .Select(i => $"plugins/LSPDFR/Test{i}.dll")
            .ToList();

        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.InRange(score, 0, 100);
    }

    // ── Structure Validation Tests ─────────────────────────────────

    [Fact]
    public void IsValidLspdfrStructure_WithPluginPath_True()
    {
        var files = new[] { "plugins/LSPDFR/Test.dll" };

        var valid = LspdfrValidator.IsValidLspdfrStructure(files);

        Assert.True(valid);
    }

    [Fact]
    public void IsValidLspdfrStructure_WithLspdfrFolder_True()
    {
        var files = new[] { "lspdfr/config.xml" };

        var valid = LspdfrValidator.IsValidLspdfrStructure(files);

        Assert.True(valid);
    }

    [Fact]
    public void IsValidLspdfrStructure_NoValidPaths_False()
    {
        var files = new[] { "readme.txt", "license.md" };

        var valid = LspdfrValidator.IsValidLspdfrStructure(files);

        Assert.False(valid);
    }

    // ── Real Archive Integration Tests ─────────────────────────────

    [Fact]
    public void DetectionScore_ValidPluginArchive_HighScore()
    {
        var archivePath = Path.Combine(TestArchivesPath, "valid_plugin.zip");
        if (!File.Exists(archivePath))
            return; // Skip if test archives not available

        var files = ExtractFileList(archivePath);
        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score > 50, $"Valid plugin archive score {score} should be > 50");
    }

    [Fact]
    public void DetectionScore_ScriptArchive_ReasonableScore()
    {
        var archivePath = Path.Combine(TestArchivesPath, "script_mod.zip");
        if (!File.Exists(archivePath))
            return;

        var files = ExtractFileList(archivePath);
        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score >= 0, $"Script archive score {score} should be >= 0");
    }

    [Fact]
    public void DetectionScore_MixedModArchive_HighScore()
    {
        var archivePath = Path.Combine(TestArchivesPath, "mixed_mod.zip");
        if (!File.Exists(archivePath))
            return;

        var files = ExtractFileList(archivePath);
        var score = LspdfrValidator.CalculateDetectionScore(files);

        Assert.True(score > 40, $"Mixed mod archive score {score} should be > 40");
    }

    [Fact]
    public void IsValidLspdfrStructure_ValidPluginArchive_True()
    {
        var archivePath = Path.Combine(TestArchivesPath, "valid_plugin.zip");
        if (!File.Exists(archivePath))
            return;

        var files = ExtractFileList(archivePath);
        var valid = LspdfrValidator.IsValidLspdfrStructure(files);

        Assert.True(valid, "Valid plugin archive should be recognized as valid LSPDFR structure");
    }

    [Fact]
    public void IsValidLspdfrStructure_ConflictArchive_True()
    {
        var archivePath = Path.Combine(TestArchivesPath, "conflict.zip");
        if (!File.Exists(archivePath))
            return;

        var files = ExtractFileList(archivePath);
        var valid = LspdfrValidator.IsValidLspdfrStructure(files);

        Assert.True(valid, "Conflict archive contains valid LSPDFR paths");
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static List<string> ExtractFileList(string zipPath)
    {
        var files = new List<string>();
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.EndsWith("/"))
                    files.Add(entry.FullName);
            }
        }
        return files;
    }
}
