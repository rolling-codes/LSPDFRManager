using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class LspdfrInstallServiceTests
{
    // ── IsLspdfrArchive ──────────────────────────────────────────────────────

    [Fact]
    public void IsLspdfrArchive_ReturnsTrueForArchiveWithRageExe()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive("RAGEPluginHook.exe", "plugins/LSPDFR.dll");
        Assert.True(LspdfrInstallService.IsLspdfrArchive(archive));
    }

    [Fact]
    public void IsLspdfrArchive_ReturnsTrueForNestedRoot()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "LSPDFR_5.1/RAGEPluginHook.exe",
            "LSPDFR_5.1/plugins/LSPDFR.dll");
        Assert.True(LspdfrInstallService.IsLspdfrArchive(archive));
    }

    [Fact]
    public void IsLspdfrArchive_ReturnsFalseForGenericMod()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive("plugins/lspdfr/SomeMod.dll");
        Assert.False(LspdfrInstallService.IsLspdfrArchive(archive));
    }

    [Fact]
    public void IsLspdfrArchive_ReturnsFalseForEmptyArchive()
    {
        var archive = FakeArchiveFactory.CreateEmptyArchive();
        Assert.False(LspdfrInstallService.IsLspdfrArchive(archive));
    }

    // ── DetectArchiveRoot ────────────────────────────────────────────────────

    [Fact]
    public void DetectArchiveRoot_StripsNestedRootWhenAllEntriesSharePrefix()
    {
        var keys = new[]
        {
            "LSPDFR_5.1/RAGEPluginHook.exe",
            "LSPDFR_5.1/plugins/LSPDFR.dll",
            "LSPDFR_5.1/lspdfr/data/backup.xml",
        };

        var root = LspdfrInstallService.DetectArchiveRoot(keys);
        Assert.Equal("LSPDFR_5.1/", root);
    }

    [Fact]
    public void DetectArchiveRoot_ReturnsEmptyWhenNoNestedRoot()
    {
        var keys = new[]
        {
            "RAGEPluginHook.exe",
            "plugins/LSPDFR.dll",
        };

        var root = LspdfrInstallService.DetectArchiveRoot(keys);
        Assert.Equal("", root);
    }

    [Fact]
    public void DetectArchiveRoot_ReturnsEmptyWhenMultipleTopLevelFolders()
    {
        var keys = new[]
        {
            "FolderA/RAGEPluginHook.exe",
            "FolderB/plugins/LSPDFR.dll",
        };

        var root = LspdfrInstallService.DetectArchiveRoot(keys);
        Assert.Equal("", root);
    }

    [Fact]
    public void DetectArchiveRoot_ReturnsEmptyWhenPrefixLacksSignatureFile()
    {
        // Nested folder but not an LSPDFR signature path
        var keys = new[]
        {
            "SomeMod/SomeMod.dll",
            "SomeMod/SomeMod.ini",
        };

        var root = LspdfrInstallService.DetectArchiveRoot(keys);
        Assert.Equal("", root);
    }

    // ── InspectArchive ───────────────────────────────────────────────────────

    [Fact]
    public void InspectArchive_ClassifiesRageExeAsRequired()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "RAGEPluginHook.exe",
            "plugins/LSPDFR.dll",
            "plugins/lspdfr/SomePlugin.dll");

        var manifest = LspdfrInstallService.InspectArchive(archive);
        var rageEntry = manifest.Entries.Single(e => e.RelativePath.Equals("RAGEPluginHook.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LspdfrEntryClassification.Required, rageEntry.Classification);
    }

    [Fact]
    public void InspectArchive_ClassifiesJunkFoldersAsIgnored()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "RAGEPluginHook.exe",
            "__MACOSX/._RAGEPluginHook.exe",
            "logs/rage.log",
            "auto-error-cache/error.dat",
            "temp/extract.tmp");

        var manifest = LspdfrInstallService.InspectArchive(archive);

        var ignored = manifest.Ignored.Select(e => e.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("__MACOSX/._RAGEPluginHook.exe", ignored);
        Assert.Contains("logs/rage.log", ignored);
        Assert.Contains("auto-error-cache/error.dat", ignored);
        Assert.Contains("temp/extract.tmp", ignored);
    }

    [Fact]
    public void InspectArchive_DetectsDoubleNestedRoot()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "LSPDFR_5.1/RAGEPluginHook.exe",
            "LSPDFR_5.1/plugins/LSPDFR.dll",
            "LSPDFR_5.1/lspdfr/data/backup.xml",
            "LSPDFR_5.1/__MACOSX/._junk");

        var manifest = LspdfrInstallService.InspectArchive(archive);
        Assert.Equal("LSPDFR_5.1/", manifest.DetectedArchiveRoot);

        // After root strip, RAGEPluginHook.exe should be Required
        var rageEntry = manifest.Entries.Single(e =>
            e.RelativePath.Equals("RAGEPluginHook.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LspdfrEntryClassification.Required, rageEntry.Classification);

        // Junk stripped entry should be Ignored
        var junk = manifest.Entries.Single(e =>
            e.RelativePath.Equals("__MACOSX/._junk", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LspdfrEntryClassification.Ignored, junk.Classification);
    }

    [Fact]
    public void InspectArchive_ReportsMissingRequiredPathsWhenAbsent()
    {
        // Archive has RAGEPluginHook.exe but not plugins/LSPDFR.dll
        var archive = FakeArchiveFactory.CreateCleanArchive("RAGEPluginHook.exe");

        var manifest = LspdfrInstallService.InspectArchive(archive);
        Assert.Contains("plugins/LSPDFR.dll", manifest.MissingRequiredPaths);
    }

    [Fact]
    public void InspectArchive_IsCompleteWhenAllRequiredPresent()
    {
        var archive = FakeArchiveFactory.CreateCleanArchive(
            "RAGEPluginHook.exe",
            "plugins/LSPDFR.dll");

        var manifest = LspdfrInstallService.InspectArchive(archive);
        Assert.True(manifest.IsComplete);
    }

    // ── ValidatePostInstall ──────────────────────────────────────────────────

    [Fact]
    public void ValidatePostInstall_ReturnsMissingWhenRequiredFilesAbsent()
    {
        var tempDir = Directory.CreateTempSubdirectory("lspdfr_test_").FullName;
        try
        {
            var result = LspdfrInstallService.ValidatePostInstall(tempDir);
            Assert.False(result.IsValid);
            Assert.Contains("RAGEPluginHook.exe", result.MissingPaths);
            Assert.Contains("plugins/LSPDFR.dll", result.MissingPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidatePostInstall_IsValidWhenAllRequiredPresent()
    {
        var tempDir = Directory.CreateTempSubdirectory("lspdfr_test_").FullName;
        try
        {
            // Write required files and directories
            File.WriteAllText(Path.Combine(tempDir, "RAGEPluginHook.exe"), "fake");
            Directory.CreateDirectory(Path.Combine(tempDir, "plugins"));
            File.WriteAllText(Path.Combine(tempDir, "plugins", "LSPDFR.dll"), "fake");
            Directory.CreateDirectory(Path.Combine(tempDir, "plugins", "lspdfr"));
            File.WriteAllText(Path.Combine(tempDir, "plugins", "lspdfr", "placeholder.txt"), "fake");
            Directory.CreateDirectory(Path.Combine(tempDir, "lspdfr"));
            File.WriteAllText(Path.Combine(tempDir, "lspdfr", "placeholder.txt"), "fake");

            var result = LspdfrInstallService.ValidatePostInstall(tempDir);
            Assert.True(result.IsValid);
            Assert.Empty(result.MissingPaths);
            Assert.Empty(result.DoubleNestedPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidatePostInstall_DetectsDoubleNesting()
    {
        var tempDir = Directory.CreateTempSubdirectory("lspdfr_test_").FullName;
        try
        {
            // Simulate double-nested install: GTA/LSPDFR/RAGEPluginHook.exe
            var nestedDir = Path.Combine(tempDir, "LSPDFR");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(nestedDir, "RAGEPluginHook.exe"), "fake");

            var result = LspdfrInstallService.ValidatePostInstall(tempDir);
            Assert.NotEmpty(result.DoubleNestedPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Junk exclusion (via InstallerSafetyPolicy) ───────────────────────────

    [Fact]
    public void InstallerSafetyPolicy_IsJunkEntry_ExcludesMacOsMetadata()
    {
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("__MACOSX/._file.dll"));
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("folder/__MACOSX/._other"));
    }

    [Fact]
    public void InstallerSafetyPolicy_IsJunkEntry_ExcludesAutoErrorCache()
    {
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("auto-error-cache/error.dat"));
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("error-cache/crash.log"));
    }

    [Fact]
    public void InstallerSafetyPolicy_IsJunkEntry_ExcludesLogAndTmpFiles()
    {
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("rageplugin.log"));
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("extract.tmp"));
        Assert.True(InstallerSafetyPolicy.IsJunkEntry("plugins/lspdfr/debug.log"));
    }

    [Fact]
    public void InstallerSafetyPolicy_IsJunkEntry_AllowsLegitimateFiles()
    {
        Assert.False(InstallerSafetyPolicy.IsJunkEntry("RAGEPluginHook.exe"));
        Assert.False(InstallerSafetyPolicy.IsJunkEntry("plugins/LSPDFR.dll"));
        Assert.False(InstallerSafetyPolicy.IsJunkEntry("plugins/lspdfr/StopThePed.dll"));
        Assert.False(InstallerSafetyPolicy.IsJunkEntry("lspdfr/data/agency.xml"));
    }
}
