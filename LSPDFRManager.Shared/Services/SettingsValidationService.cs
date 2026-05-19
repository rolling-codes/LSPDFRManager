using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class SettingsValidationService
{
    public List<SettingsValidationResult> Validate()
    {
        var results = new List<SettingsValidationResult>();
        var cfg = AppConfig.Instance;

        if (string.IsNullOrWhiteSpace(cfg.GtaPath))
            results.Add(new SettingsValidationResult { SettingName = "GTA V Path", Issue = "Not configured.", SuggestedFix = "Set the GTA V folder in Settings.", IsBlocking = true });
        else if (!Directory.Exists(cfg.GtaPath))
            results.Add(new SettingsValidationResult { SettingName = "GTA V Path", Issue = "Folder does not exist.", SuggestedFix = "Check that the GTA V installation folder is accessible.", IsBlocking = true });
        else if (LspdfrInstallLocator.FindGtaExe(cfg.GtaPath) is null)
            results.Add(new SettingsValidationResult { SettingName = "GTA V Path", Issue = "GTA V executable not found.", SuggestedFix = "Verify the path points to the GTA V installation folder.", IsBlocking = true });

        if (string.IsNullOrWhiteSpace(cfg.BackupPath))
            results.Add(new SettingsValidationResult { SettingName = "Backup Path", Issue = "Not configured.", SuggestedFix = "Set a backup folder in Settings.", IsBlocking = false });
        else if (!IsWritable(cfg.BackupPath))
            results.Add(new SettingsValidationResult { SettingName = "Backup Path", Issue = "Folder is not writable.", SuggestedFix = "Choose a folder with write permissions.", IsBlocking = false });

        if (!IsWritable(AppDataPaths.Root))
            results.Add(new SettingsValidationResult { SettingName = "App Data Folder", Issue = "App data folder is not writable.", SuggestedFix = "Run as administrator or check permissions.", IsBlocking = true });

        AddDiskSpaceResult(results, AppDataPaths.Root, "App Data Folder", true);
        if (!string.IsNullOrWhiteSpace(cfg.GtaPath) && Directory.Exists(cfg.GtaPath))
            AddDiskSpaceResult(results, cfg.GtaPath, "GTA V Path", true);

        if (cfg.AutoStartBrowseApi && !string.IsNullOrWhiteSpace(cfg.BrowseApiPath) && !File.Exists(cfg.BrowseApiPath))
            results.Add(new SettingsValidationResult { SettingName = "Browse API Path", Issue = "Configured executable not found.", SuggestedFix = "Check the Browse API path in Settings.", IsBlocking = false });

        if (!string.IsNullOrWhiteSpace(cfg.BrowseApiBaseUrl) && !Uri.TryCreate(cfg.BrowseApiBaseUrl, UriKind.Absolute, out _))
            results.Add(new SettingsValidationResult { SettingName = "Browse API URL", Issue = "Invalid URL format.", SuggestedFix = "Use format: http://localhost:5284", IsBlocking = false });

        return results;
    }

    private static bool IsWritable(string folderPath)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            var test = Path.Combine(folderPath, $".write_test_{Guid.NewGuid()}");
            File.WriteAllText(test, "");
            File.Delete(test);
            return true;
        }
        catch { return false; }
    }

    private static void AddDiskSpaceResult(List<SettingsValidationResult> results, string path, string settingName, bool isBlocking)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
                return;

            var drive = new DriveInfo(root);
            var requiredBytes = (long)AppConfig.Instance.MinimumFreeDiskSpaceMb * 1024 * 1024;
            if (drive.AvailableFreeSpace < requiredBytes)
            {
                results.Add(new SettingsValidationResult
                {
                    SettingName = settingName,
                    Issue = $"Only {drive.AvailableFreeSpace / 1024 / 1024:N0} MB free.",
                    SuggestedFix = $"Free at least {AppConfig.Instance.MinimumFreeDiskSpaceMb:N0} MB before installing or backing up mods.",
                    IsBlocking = isBlocking
                });
            }
        }
        catch
        {
        }
    }
}
