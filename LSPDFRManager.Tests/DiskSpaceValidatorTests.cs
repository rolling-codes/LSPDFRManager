using LSPDFRManager.OpenIv.CarInstall;
using LSPDFRManager.OpenIv.CarInstall.Models;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for pre-flight disk space validation.
/// Verifies fail-fast behavior before execution.
/// </summary>
public class DiskSpaceValidatorTests
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"disk_validator_{Guid.NewGuid():N}");

    public DiskSpaceValidatorTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void EnsureSufficientSpace_SufficientSpace_Passes()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "test_car",
            Operations = new()
            {
                new() { SourcePath = "models/car.yft", DestinationPath = @"mods\models\car.yft", Overwrite = true },
                new() { SourcePath = "textures/car.ytd", DestinationPath = @"mods\textures\car.ytd", Overwrite = true }
            },
            XmlPatches = new()
        };

        // Should not throw
        DiskSpaceValidator.EnsureSufficientSpace(plan, archive, _tempRoot);
    }

    [Fact]
    public void EnsureSufficientSpace_EmptyPlan_Passes()
    {
        var archive = FakeArchiveFactory.CreateEmptyArchive();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "empty",
            Operations = new(),
            XmlPatches = new()
        };

        // Should not throw
        DiskSpaceValidator.EnsureSufficientSpace(plan, archive, _tempRoot);
    }

    [Fact]
    public void EnsureSufficientSpace_MissingArchiveEntry_ZeroBytesRequired()
    {
        var archive = FakeArchiveFactory.CreateEmptyArchive();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "missing",
            Operations = new()
            {
                new() { SourcePath = "nonexistent.yft", DestinationPath = @"mods\nonexistent.yft", Overwrite = true }
            },
            XmlPatches = new()
        };

        // Should not throw (archive entry not found = 0 bytes required)
        DiskSpaceValidator.EnsureSufficientSpace(plan, archive, _tempRoot);
    }

    [Fact]
    public void EnsureSufficientSpace_InvalidDriveLetter_ThrowsInvalidOperationException()
    {
        var archive = FakeArchiveFactory.CreateReplaceVehicle();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "bad_drive",
            Operations = new()
            {
                new() { SourcePath = "models/car.yft", DestinationPath = @"Z:\mods\car.yft", Overwrite = true }
            },
            XmlPatches = new()
        };

        // Z: drive doesn't exist, should throw
        var ex = Assert.Throws<InvalidOperationException>(
            () => DiskSpaceValidator.EnsureSufficientSpace(plan, archive, @"Z:\fake\path"));

        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void EnsureSufficientSpace_CalculatesWithSafetyBuffer()
    {
        var archive = FakeArchiveFactory.CreateLargeFileArchive();
        var plan = new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = "large",
            Operations = new()
            {
                new() { SourcePath = "large.dat", DestinationPath = @"mods\large.dat", Overwrite = true }
            },
            XmlPatches = new()
        };

        // 10MB archive * 1.1 (buffer) = 11MB required
        // Most systems have >11MB free, so this should pass
        DiskSpaceValidator.EnsureSufficientSpace(plan, archive, _tempRoot);
    }
}
