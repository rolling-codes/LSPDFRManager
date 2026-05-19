using System.Diagnostics;
using System.Security.Cryptography;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class VersionDetectorService
{
    private static readonly string[] ShvdnCandidates =
    [
        "ScriptHookVDotNet3.dll",
        "ScriptHookVDotNet2.dll",
        "ScriptHookVDotNet.asi",
    ];

    public Task<VersionBundle> DetectAsync(string gtaPath) =>
        Task.Run(() => Detect(gtaPath));

    private static VersionBundle Detect(string gtaPath)
    {
        var gta5      = LspdfrInstallLocator.FindGtaExe(gtaPath) ?? Path.Combine(gtaPath, "GTA5.exe");
        var lspdfr    = LspdfrInstallLocator.FindLspdfrCore(gtaPath);
        var rph       = LspdfrInstallLocator.FindRagePluginHook(gtaPath);
        var shv       = Path.Combine(gtaPath, "ScriptHookV.dll");
        var shvdnPath = ResolveShvdn(gtaPath);

        var gta5Info = File.Exists(gta5) ? new FileInfo(gta5) : null;

        return new VersionBundle
        {
            GtaPresent             = gta5Info is not null,
            LspdfrPresent          = lspdfr is not null || LspdfrInstallLocator.IsLspdfrInstalled(gtaPath),
            RagePluginHookPresent  = rph is not null,

            GtaExeFileSizeBytes    = gta5Info?.Length,
            GtaExeLastWriteTimeUtc = gta5Info?.LastWriteTimeUtc,

            GtaVersion             = ReadVersion(gta5),
            LspdfrVersion          = lspdfr is not null ? ReadVersion(lspdfr) : null,
            RagePluginHookVersion  = rph is not null ? ReadVersion(rph) : null,
            ScriptHookVVersion     = ReadVersion(shv),
            ScriptHookVDotNetVersion = shvdnPath is not null ? ReadVersion(shvdnPath) : null,

            // GTA5.exe is ~100 MB — skip hash (TryComputeHash returns null for files >50 MB)
            GtaHash                = null,
            LspdfrHash             = lspdfr is not null ? TryComputeHash(lspdfr) : null,
            RagePluginHookHash     = rph is not null ? TryComputeHash(rph) : null,
            ScriptHookVHash        = TryComputeHash(shv),
            ScriptHookVDotNetHash  = shvdnPath is not null ? TryComputeHash(shvdnPath) : null,
        };
    }

    private static string? ResolveShvdn(string gtaPath)
    {
        foreach (var candidate in ShvdnCandidates)
        {
            var full = Path.Combine(gtaPath, candidate);
            if (File.Exists(full))
                return full;
        }
        return null;
    }

    private static string? ReadVersion(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryComputeHash(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 50 * 1024 * 1024)
                return null;

            using var fs = File.OpenRead(path);
            var bytes = SHA256.HashData(fs);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
