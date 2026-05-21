using LSPDFRManager.Core;
using LSPDFRManager.OpenIv.CarInstall.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.OpenIv.CarInstall;

public class OpenIvInstallPlanner
{
    public OpenIvInstallPlan BuildPlan(
        CarInstallType type,
        IArchive archive,
        string modName)
    {
        return type switch
        {
            CarInstallType.ReplaceVehicle => BuildReplacePlan(archive, modName),
            CarInstallType.AddonDLC => BuildAddonPlan(archive, modName),
            CarInstallType.ConfigPatch => BuildConfigPlan(archive, modName),
            _ => throw new InvalidOperationException($"Unknown install type: {type}")
        };
    }

    private OpenIvInstallPlan BuildReplacePlan(IArchive archive, string modName)
    {
        var ops = archive.Entries
            .Where(e => !e.IsDirectory && (e.Key.EndsWith(".yft", StringComparison.OrdinalIgnoreCase) ||
                                           e.Key.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)))
            .Select(e => new FileOperation
            {
                SourcePath = e.Key,
                DestinationPath = Path.Combine("mods", "update", "x64", "dlcpacks", "patchday", Path.GetFileName(e.Key)),
                Overwrite = true
            })
            .ToList();

        return new OpenIvInstallPlan
        {
            Type = CarInstallType.ReplaceVehicle,
            TargetDlcName = modName,
            Operations = ops
        };
    }

    private OpenIvInstallPlan BuildAddonPlan(IArchive archive, string modName)
    {
        var entries = archive.Entries
            .Where(e => !e.IsDirectory)
            .ToList();

        var dlcRoot = DetectDlcRoot(entries.Select(e => e.Key), modName);
        var dlcName = dlcRoot.DlcName;

        var ops = entries
            .Select(e => new FileOperation
            {
                SourcePath = e.Key,
                DestinationPath = Path.Combine("mods", "update", "x64", "dlcpacks", dlcName, StripDlcRoot(e.Key, dlcRoot.RootPrefix)).Replace("/", @"\"),
                Overwrite = true
            })
            .ToList();

        var xmlPatches = new List<XmlPatch>
        {
            new XmlPatch
            {
                FilePath = Path.Combine("mods", "update", "update.rpf", "common", "data", "dlclist.xml"),
                XPath = "/SMandatoryPacksData/Paths",
                Value = $"dlcpacks:/{dlcName}/"
            }
        };

        return new OpenIvInstallPlan
        {
            Type = CarInstallType.AddonDLC,
            TargetDlcName = dlcName,
            Operations = ops,
            XmlPatches = xmlPatches
        };
    }

    private BuildDlcRoot DetectDlcRoot(IEnumerable<string> entryKeys, string fallbackModName)
    {
        foreach (var key in entryKeys)
        {
            var parts = SplitPath(key);
            var dlcRpfIndex = Array.FindIndex(parts, part => part.Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));
            if (dlcRpfIndex < 0)
                continue;

            var dlcpacksIndex = Array.FindIndex(parts, part => part.Equals("dlcpacks", StringComparison.OrdinalIgnoreCase));
            if (dlcpacksIndex >= 0 && dlcpacksIndex + 1 < dlcRpfIndex)
            {
                var dlcName = parts[dlcpacksIndex + 1];
                return new BuildDlcRoot(dlcName, string.Join('/', parts.Take(dlcpacksIndex + 2)) + "/");
            }

            if (dlcRpfIndex > 0)
            {
                var dlcName = parts[dlcRpfIndex - 1];
                return new BuildDlcRoot(dlcName, string.Join('/', parts.Take(dlcRpfIndex)) + "/");
            }

            return new BuildDlcRoot(SanitizeDlcName(fallbackModName), string.Empty);
        }

        return new BuildDlcRoot(SanitizeDlcName(fallbackModName), string.Empty);
    }

    private static string StripDlcRoot(string key, string rootPrefix)
    {
        var normalized = NormalizePath(key);
        if (!string.IsNullOrWhiteSpace(rootPrefix) && normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return normalized[rootPrefix.Length..];

        return normalized;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string[] SplitPath(string path) =>
        NormalizePath(path).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string SanitizeDlcName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Trim().Where(ch => !invalid.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "addonpack" : sanitized;
    }

    private readonly record struct BuildDlcRoot(string DlcName, string RootPrefix);

    private OpenIvInstallPlan BuildConfigPlan(IArchive archive, string modName)
    {
        var ops = archive.Entries
            .Where(e => !e.IsDirectory && e.Key.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .Select(e => new FileOperation
            {
                SourcePath = e.Key,
                DestinationPath = Path.Combine("mods", e.Key).Replace("/", @"\"),
                Overwrite = true
            })
            .ToList();

        return new OpenIvInstallPlan
        {
            Type = CarInstallType.ConfigPatch,
            TargetDlcName = modName,
            Operations = ops
        };
    }
}
