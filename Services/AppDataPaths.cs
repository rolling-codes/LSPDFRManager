namespace LSPDFRManager.Services;

public static class AppDataPaths
{
    public static string Root =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LSPDFRManager");

    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string LibraryFile => Path.Combine(Root, "library.json");
    public static string ConfigSnapshotsFile => Path.Combine(Root, "configs.json");
    public static string LogFile => Path.Combine(Root, "app.log");
    public static string KeysDirectory => Path.Combine(Root, "keys");

    public static void EnsureRootExists() => Directory.CreateDirectory(Root);
}
