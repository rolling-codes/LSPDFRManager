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
        var ops = archive.Entries
            .Where(e => !e.IsDirectory)
            .Select(e => new FileOperation
            {
                SourcePath = e.Key,
                DestinationPath = Path.Combine("mods", "update", "x64", "dlcpacks", modName, e.Key).Replace("/", @"\"),
                Overwrite = true
            })
            .ToList();

        var xmlPatches = new List<XmlPatch>
        {
            new XmlPatch
            {
                FilePath = Path.Combine("mods", "update", "update.rpf", "common", "data", "dlclist.xml"),
                XPath = "/SMandatoryPacksData/Paths",
                Value = $"dlcpacks:/{modName}/"
            }
        };

        return new OpenIvInstallPlan
        {
            Type = CarInstallType.AddonDLC,
            TargetDlcName = modName,
            Operations = ops,
            XmlPatches = xmlPatches
        };
    }

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
