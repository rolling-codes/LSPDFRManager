using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class FileInstallerSafetyPolicyTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"install_safety_{Guid.NewGuid():N}");

    public FileInstallerSafetyPolicyTests()
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
        catch { }
    }

    [Fact]
    public async Task KeepExistingAction_LeavesOriginalUnchanged()
    {
        var target = Path.Combine(_tempRoot, "plugins", "lspdfr", "UltimateBackup.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "original");

        var archive = new FakeArchive([
            new FakeArchiveEntry("plugins/lspdfr/UltimateBackup.ini", System.Text.Encoding.UTF8.GetBytes("incoming"))
        ]);

        var plan = new InstallPlan
        {
            Entries =
            [
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/UltimateBackup.ini",
                    TargetPath = target,
                    DestinationExists = true,
                    PlannedAction = InstallConflictAction.KeepExisting,
                }
            ]
        };

        var result = await FileInstaller.InstallAsync(archive, _tempRoot, plan);

        Assert.True(result.Success);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public async Task BackupAndReplaceAction_CreatesTimestampedBackup_AndSecondReplaceKeepsFirstBackup()
    {
        var target = Path.Combine(_tempRoot, "plugins", "lspdfr", "StopThePed.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "v1");

        var plan = new InstallPlan
        {
            Entries =
            [
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/StopThePed.dll",
                    TargetPath = target,
                    DestinationExists = true,
                    PlannedAction = InstallConflictAction.BackupAndReplace,
                }
            ]
        };

        var archive1 = new FakeArchive([
            new FakeArchiveEntry("plugins/lspdfr/StopThePed.dll", System.Text.Encoding.UTF8.GetBytes("v2"))
        ]);

        var result1 = await FileInstaller.InstallAsync(archive1, _tempRoot, plan);
        Assert.True(result1.Success);

        var archive2 = new FakeArchive([
            new FakeArchiveEntry("plugins/lspdfr/StopThePed.dll", System.Text.Encoding.UTF8.GetBytes("v3"))
        ]);

        var result2 = await FileInstaller.InstallAsync(archive2, _tempRoot, plan);
        Assert.True(result2.Success);

        var backupFiles = Directory.GetFiles(Path.GetDirectoryName(target)!, "StopThePed.dll.bak.*", SearchOption.TopDirectoryOnly);
        Assert.True(backupFiles.Length >= 2);
        Assert.Equal("v3", File.ReadAllText(target));
    }

    [Fact]
    public async Task RenameIncomingAction_CreatesIncomingVariant()
    {
        var target = Path.Combine(_tempRoot, "plugins", "lspdfr", "UltimateBackup", "DefaultRegions.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "<old />");

        var archive = new FakeArchive([
            new FakeArchiveEntry("plugins/lspdfr/UltimateBackup/DefaultRegions.xml", System.Text.Encoding.UTF8.GetBytes("<incoming />"))
        ]);

        var plan = new InstallPlan
        {
            Entries =
            [
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/UltimateBackup/DefaultRegions.xml",
                    TargetPath = target,
                    DestinationExists = true,
                    PlannedAction = InstallConflictAction.RenameIncoming,
                }
            ]
        };

        var result = await FileInstaller.InstallAsync(archive, _tempRoot, plan);

        Assert.True(result.Success);
        Assert.Equal("<old />", File.ReadAllText(target));

        var incoming = Directory.GetFiles(Path.GetDirectoryName(target)!, "DefaultRegions.incoming*.xml", SearchOption.TopDirectoryOnly);
        Assert.Single(incoming);
        Assert.Equal("<incoming />", File.ReadAllText(incoming[0]));
    }

    [Fact]
    public async Task FailedInstall_RestoresOriginalOverwrittenFile_AndRemovesNewFiles()
    {
        var existingDll = Path.Combine(_tempRoot, "plugins", "lspdfr", "existing.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(existingDll)!);
        File.WriteAllText(existingDll, "old");

        var archive = new FakeArchive(new IArchiveEntry[]
        {
            new FakeArchiveEntry("plugins/lspdfr/existing.dll", new byte[] { 1, 2, 3 }),
            new FakeArchiveEntry("plugins/lspdfr/newfile.dll", new byte[] { 4, 5, 6 }),
            new FakeArchiveEntry("plugins/lspdfr/fail.dll", () => throw new IOException("stream failure")),
        });

        var plan = new InstallPlan
        {
            Entries =
            [
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/existing.dll",
                    TargetPath = existingDll,
                    DestinationExists = true,
                    PlannedAction = InstallConflictAction.BackupAndReplace,
                },
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/newfile.dll",
                    TargetPath = Path.Combine(_tempRoot, "plugins", "lspdfr", "newfile.dll"),
                    DestinationExists = false,
                    PlannedAction = InstallConflictAction.BackupAndReplace,
                },
                new InstallPlanEntry
                {
                    ArchivePath = "plugins/lspdfr/fail.dll",
                    TargetPath = Path.Combine(_tempRoot, "plugins", "lspdfr", "fail.dll"),
                    DestinationExists = false,
                    PlannedAction = InstallConflictAction.BackupAndReplace,
                },
            ]
        };

        var result = await FileInstaller.InstallAsync(archive, _tempRoot, plan);

        Assert.False(result.Success);
        Assert.True(result.IsPartial);
        Assert.Equal("old", File.ReadAllText(existingDll));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "plugins", "lspdfr", "newfile.dll")));
    }

    [Fact]
    public async Task PathSafetyTraversalBlock_HappensBeforeOverwriteAndPreservesExistingFile()
    {
        var existing = Path.Combine(_tempRoot, "legitimate.dll");
        File.WriteAllText(existing, "original");

        var archive = new FakeArchive(new IArchiveEntry[]
        {
            new FakeArchiveEntry("legitimate.dll", new byte[] { 7, 8, 9 }),
            new FakeArchiveEntry("../../escape.dll", new byte[] { 1, 2, 3 }),
        });

        var plan = new InstallPlan
        {
            Entries =
            [
                new InstallPlanEntry
                {
                    ArchivePath = "legitimate.dll",
                    TargetPath = existing,
                    DestinationExists = true,
                    PlannedAction = InstallConflictAction.BackupAndReplace,
                }
            ]
        };

        var result = await FileInstaller.InstallAsync(archive, _tempRoot, plan);

        Assert.False(result.Success);
        Assert.Equal("original", File.ReadAllText(existing));

        var parentEscape = Path.Combine(Path.GetDirectoryName(_tempRoot)!, "escape.dll");
        Assert.False(File.Exists(parentEscape));
    }
}
