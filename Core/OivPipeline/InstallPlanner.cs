using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class InstallPlanner
{
    public static IReadOnlyList<InstallOperation> Plan(
        ClassificationResult classification,
        IReadOnlyList<BundleFile> files,
        BundleManifest manifest)
    {
        if (!classification.IsClassified)
            throw new InvalidOperationException("Cannot plan installation for an unclassified bundle.");

        var ops = classification.Type switch
        {
            BundleType.VehicleAddon   => PlanVehicleAddon(files, manifest),
            BundleType.VehicleReplace => PlanVehicleReplace(files, manifest),
            BundleType.Els            => PlanEls(files, manifest),
            BundleType.RphPlugin      => PlanRphPlugin(files, manifest),
            BundleType.ShvdnScript    => PlanShvdnScript(files, manifest),
            BundleType.SirenPack      => PlanSirenPack(files, manifest),
            BundleType.WeaponAddon    => PlanWeaponAddon(files, manifest),
            _                         => new List<InstallOperation>()
        };

        var deduped = DeduplicatePatches(ops);

        return deduped
            .OrderBy(op => op switch
            {
                CopyOperation c          => c.TargetGamePath,
                PatchXmlOperation p      => p.TargetGamePath,
                EnsureModsCopyOperation e => e.TargetArchivePath,
                _                        => string.Empty
            }, StringComparer.Ordinal)
            .ToList();
    }

    private static List<InstallOperation> PlanVehicleAddon(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var dlcPackName = manifest.DlcPackName!;
        var dlcBase = $"mods/update/x64/dlcpacks/{dlcPackName}";
        var rootPrefix = DetermineRootPrefix(files, manifest.SourceFolder, dlcPackName);

        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            var strippedPath = rootPrefix is not null && file.RelativePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? file.RelativePath[rootPrefix.Length..]
                : file.RelativePath;
            var targetPath = $"{dlcBase}/{strippedPath}";
            ops.Add(new CopyOperation(file.RelativePath, targetPath, true));
        }

        ops.Add(new PatchXmlOperation(
            "mods/update/update.rpf/common/data/dlclist.xml",
            $"<Item>dlcpacks:/{dlcPackName}/</Item>",
            $"dlcpacks:/{dlcPackName}/"));

        return ops;
    }

    private static List<InstallOperation> PlanVehicleReplace(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var targetArchivePath = manifest.TargetArchivePath
            ?? BundleValidator.KnownSlotMapLookup(manifest.ReplaceSlot!);

        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            if (file.Extension.Equals(".yft", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".ytd", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new CopyOperation(
                    file.RelativePath,
                    $"{targetArchivePath}/{Path.GetFileName(file.RelativePath)}",
                    true));
            }
        }

        return ops;
    }

    private static List<InstallOperation> PlanEls(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            if (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new CopyOperation(
                    file.RelativePath,
                    $"ELS/pack_default/{Path.GetFileName(file.RelativePath)}",
                    true));
            }
        }

        return ops;
    }

    private static List<InstallOperation> PlanRphPlugin(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new CopyOperation(
                    file.RelativePath,
                    $"plugins/{Path.GetFileName(file.RelativePath)}",
                    true));
            }
        }

        return ops;
    }

    private static List<InstallOperation> PlanShvdnScript(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new CopyOperation(
                    file.RelativePath,
                    $"scripts/{Path.GetFileName(file.RelativePath)}",
                    true));
            }
        }

        return ops;
    }

    private static List<InstallOperation> PlanSirenPack(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            ops.Add(new CopyOperation(file.RelativePath, file.RelativePath, true));
        }

        return ops;
    }

    private static List<InstallOperation> PlanWeaponAddon(IReadOnlyList<BundleFile> files, BundleManifest manifest)
    {
        var dlcPackName = manifest.DlcPackName!;
        var dlcBase = $"mods/update/x64/dlcpacks/{dlcPackName}";
        var rootPrefix = DetermineRootPrefix(files, manifest.SourceFolder, dlcPackName);

        var ops = new List<InstallOperation>();

        foreach (var file in files)
        {
            var strippedPath = rootPrefix is not null && file.RelativePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? file.RelativePath[rootPrefix.Length..]
                : file.RelativePath;
            var targetPath = $"{dlcBase}/{strippedPath}";
            ops.Add(new CopyOperation(file.RelativePath, targetPath, true));
        }

        ops.Add(new PatchXmlOperation(
            "mods/update/update.rpf/common/data/dlclist.xml",
            $"<Item>dlcpacks:/{dlcPackName}/</Item>",
            $"dlcpacks:/{dlcPackName}/"));

        return ops;
    }

    private static string? DetermineRootPrefix(IReadOnlyList<BundleFile> files, string? sourceFolder, string dlcPackName)
    {
        if (sourceFolder is not null)
            return sourceFolder.Replace('\\', '/').TrimEnd('/') + "/";

        foreach (var file in files)
        {
            var parts = file.RelativePath.Split('/');
            var packIdx = Array.FindIndex(parts, p => p.Equals(dlcPackName, StringComparison.OrdinalIgnoreCase));
            if (packIdx >= 0)
                return string.Join('/', parts.Take(packIdx + 1)) + "/";
        }

        foreach (var file in files)
        {
            var parts = file.RelativePath.Split('/');
            var dlcpacksIdx = Array.FindIndex(parts, p => p.Equals("dlcpacks", StringComparison.OrdinalIgnoreCase));
            if (dlcpacksIdx >= 0 && dlcpacksIdx + 1 < parts.Length)
                return string.Join('/', parts.Take(dlcpacksIdx + 2)) + "/";
        }

        return null;
    }

    private static List<InstallOperation> DeduplicatePatches(List<InstallOperation> ops)
    {
        var result = new List<InstallOperation>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var op in ops)
        {
            if (op is PatchXmlOperation patch)
            {
                if (seenKeys.Add(patch.IdempotencyKey))
                    result.Add(op);
            }
            else
            {
                result.Add(op);
            }
        }

        return result;
    }
}
