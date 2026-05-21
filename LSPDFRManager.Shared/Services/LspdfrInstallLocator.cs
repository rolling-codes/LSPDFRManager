namespace LSPDFRManager.Services;

public static class LspdfrInstallLocator
{
    public static readonly string[] GtaExeCandidates =
    [
        "GTA5.exe",
        "GTA5_BE.exe",
        "PlayGTAV.exe",
    ];

    public static readonly string[] LspdfrCoreCandidates =
    [
        @"plugins\LSPD First Response.dll",
        @"plugins\LSPDFR.dll",
        @"plugins\lspdfr\LSPDFR.dll",
    ];

    public static readonly string[] LspdfrFolderCandidates =
    [
        "lspdfr",
        @"plugins\lspdfr",
    ];

    public static readonly string[] LspdfrToolCandidates =
    [
        @"lspdfr\LSPDFR Configurator.exe",
        @"lspdfr\LSPDFR.exe",
    ];

    public static readonly string[] RagePluginHookCandidates =
    [
        "RAGEPluginHook.exe",
    ];

    public static string? FindGtaExe(string gtaPath) => FindExistingFile(gtaPath, GtaExeCandidates);

    public static string? FindLspdfrCore(string gtaPath) => FindExistingFile(gtaPath, LspdfrCoreCandidates);

    public static string? FindLspdfrFolder(string gtaPath) => FindExistingDirectory(gtaPath, LspdfrFolderCandidates);

    public static string? FindLspdfrTool(string gtaPath) => FindExistingFile(gtaPath, LspdfrToolCandidates);

    public static string? FindRagePluginHook(string gtaPath) => FindExistingFile(gtaPath, RagePluginHookCandidates);

    public static bool IsGtaInstalled(string gtaPath) => FindGtaExe(gtaPath) is not null;

    public static bool IsLspdfrInstalled(string gtaPath) =>
        FindLspdfrCore(gtaPath) is not null ||
        FindLspdfrFolder(gtaPath) is not null ||
        FindLspdfrTool(gtaPath) is not null;

    public static bool IsRagePluginHookInstalled(string gtaPath) =>
        FindRagePluginHook(gtaPath) is not null &&
        File.Exists(Path.Combine(gtaPath, "RagePluginHook.dll"));

    public static string ToRelativePath(string gtaPath, string fullPath)
    {
        try
        {
            return Path.GetRelativePath(gtaPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        }
        catch
        {
            return fullPath;
        }
    }

    private static string? FindExistingFile(string gtaPath, IEnumerable<string> relativeCandidates)
    {
        if (string.IsNullOrWhiteSpace(gtaPath))
            return null;

        foreach (var relative in relativeCandidates)
        {
            var full = Path.Combine(gtaPath, Normalize(relative));
            if (File.Exists(full))
                return full;
        }

        return null;
    }

    private static string? FindExistingDirectory(string gtaPath, IEnumerable<string> relativeCandidates)
    {
        if (string.IsNullOrWhiteSpace(gtaPath))
            return null;

        foreach (var relative in relativeCandidates)
        {
            var full = Path.Combine(gtaPath, Normalize(relative));
            if (Directory.Exists(full))
                return full;
        }

        return null;
    }

    private static string Normalize(string relativePath) =>
        relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}
