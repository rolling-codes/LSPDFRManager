namespace LSPDFRManager.Services;

public static class AppDataPaths
{
    private static string? _overrideRoot;
    public static void OverrideRoot(string path) => _overrideRoot = path;
    public static void ClearOverride() => _overrideRoot = null;

    public static string Root =>
        _overrideRoot ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LSPDFRManager");

    public static string ConfigFile            => Path.Combine(Root, "config.json");
    public static string LibraryFile           => Path.Combine(Root, "library.json");
    public static string ConfigSnapshotsFile   => Path.Combine(Root, "configs.json");
    public static string LogFile               => Path.Combine(Root, "app.log");
    public static string KeysDirectory         => Path.Combine(Root, "keys");
    public static string ProfilesDirectory     => Path.Combine(Root, "profiles");
    public static string RestorePointsDirectory => Path.Combine(Root, "restore_points");
    public static string RestorePointsIndex    => Path.Combine(RestorePointsDirectory, "index.json");
    public static string ChangeHistoryFile     => Path.Combine(Root, "data", "change_history.json");
    public static string ModMetadataFile       => Path.Combine(Root, "data", "mod_metadata.json");
    public static string BackupManifestFile    => Path.Combine(Root, "data", "backup_manifest.json");
    public static string BrowseApiLogFile      => Path.Combine(Root, "logs", "browse_api_service.log");

    public static void EnsureRootExists()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "data"));
        Directory.CreateDirectory(Path.Combine(Root, "logs"));
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(RestorePointsDirectory);
    }
}
