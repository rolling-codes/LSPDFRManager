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
        var gta5      = Path.Combine(gtaPath, "GTA5.exe");
        var lspdfr    = Path.Combine(gtaPath, "plugins", "LSPDFR.dll");
        var rph       = Path.Combine(gtaPath, "RAGEPluginHook.exe");
        var shv       = Path.Combine(gtaPath, "ScriptHookV.dll");
        var shvdnPath = ResolveShvdn(gtaPath);

        var gta5Info = File.Exists(gta5) ? new FileInfo(gta5) : null;

        return new VersionBundle
        {
            GtaPresent             = gta5Info is not null,
            LspdfrPresent          = File.Exists(lspdfr),
            RagePluginHookPresent  = File.Exists(rph),

            GtaExeFileSizeBytes    = gta5Info?.Length,
            GtaExeLastWriteTimeUtc = gta5Info?.LastWriteTimeUtc,

            GtaVersion             = ReadVersion(gta5),
            LspdfrVersion          = ReadVersion(lspdfr),
            RagePluginHookVersion  = ReadVersion(rph),
            ScriptHookVVersion     = ReadVersion(shv),
            ScriptHookVDotNetVersion = shvdnPath is not null ? ReadVersion(shvdnPath) : null,

            // GTA5.exe is ~100 MB — skip hash (TryComputeHash returns null for files >50 MB)
            GtaHash                = null,
            LspdfrHash             = TryComputeHash(lspdfr),
            RagePluginHookHash     = TryComputeHash(rph),
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
