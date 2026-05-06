using LSPDFRManager.Domain;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>Tests for <see cref="AppConfig"/> serialisation and defaults.</summary>
public class AppConfigTests : IDisposable
{
    // Write to a temp file so tests never touch the real config.
    private readonly string _tempPath = Path.Combine(
        Path.GetTempPath(), $"lspm_test_config_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public void DefaultGtaPath_ContainsGrandTheftAuto()
    {
        var config = new AppConfig();
        Assert.Contains("Grand Theft Auto", config.GtaPath);
    }

    [Fact]
    public void DefaultAutoBackup_IsTrue()
    {
        var config = new AppConfig();
        Assert.True(config.AutoBackupOnInstall);
    }

    [Fact]
    public void DefaultConfirmUninstall_IsTrue()
    {
        var config = new AppConfig();
        Assert.True(config.ConfirmBeforeUninstall);
    }

    [Fact]
    public void DefaultAutoLaunchAfterInstall_IsFalse()
    {
        var config = new AppConfig();
        Assert.False(config.AutoLaunchAfterInstall);
    }

    [Fact]
    public void DefaultLastBackupDate_IsNull()
    {
        var config = new AppConfig();
        Assert.Null(config.LastBackupDate);
    }

    [Fact]
    public void SaveAndLoad_RoundtripsAllFields()
    {
        // Build a config with non-default values and serialise manually.
        var original = new AppConfig
        {
            GtaPath = @"D:\Games\GTA5",
            BackupPath = @"D:\Backups",
            AutoBackupOnInstall = false,
            ConfirmBeforeUninstall = false,
            AutoLaunchAfterInstall = true,
            LastBackupDate = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
        };

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var json = System.Text.Json.JsonSerializer.Serialize(original, options);
        File.WriteAllText(_tempPath, json);

        var loaded = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(
            File.ReadAllText(_tempPath))!;

        Assert.Equal(original.GtaPath, loaded.GtaPath);
        Assert.Equal(original.BackupPath, loaded.BackupPath);
        Assert.Equal(original.AutoBackupOnInstall, loaded.AutoBackupOnInstall);
        Assert.Equal(original.ConfirmBeforeUninstall, loaded.ConfirmBeforeUninstall);
        Assert.Equal(original.AutoLaunchAfterInstall, loaded.AutoLaunchAfterInstall);
        Assert.Equal(original.LastBackupDate, loaded.LastBackupDate);
    }
}
