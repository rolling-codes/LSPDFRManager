using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class BundleClassifier
{
    public static ClassificationResult Classify(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        if (manifest is not null)
        {
            var type = ParseBundleType(manifest.Type);
            if (type is null)
                return Refuse($"Manifest declares unsupported type '{manifest.Type}'.");
            return new ClassificationResult { Type = type, Source = ConfidenceSource.Manifest };
        }

        var candidates = DetectHardSignatures(files);

        return candidates.Count switch
        {
            0 => Refuse("No manifest and no recognizable hard signatures. Cannot classify bundle."),
            1 => new ClassificationResult { Type = candidates[0], Source = ConfidenceSource.HardSignature },
            _ => Refuse($"Ambiguous signatures: bundle matches multiple types ({string.Join(", ", candidates)}). Provide a manifest.json to disambiguate.")
        };
    }

    private static List<BundleType> DetectHardSignatures(IReadOnlyList<BundleFile> files)
    {
        var candidates = new List<BundleType>();

        if (HasDlcRpfInDlcpacksStructure(files))
            candidates.Add(BundleType.VehicleAddon);

        if (HasReplaceVehicleSignature(files))
            candidates.Add(BundleType.VehicleReplace);

        if (HasElsSignature(files))
            candidates.Add(BundleType.Els);

        if (HasWeaponAddonSignature(files))
            candidates.Add(BundleType.WeaponAddon);

        return candidates;
    }

    private static bool HasDlcRpfInDlcpacksStructure(IReadOnlyList<BundleFile> files)
    {
        foreach (var file in files)
        {
            var segments = file.RelativePath.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Segment must be exactly "dlc.rpf" (already matched case-insensitively above;
                // per spec the segment at index must be "dlc.rpf" exactly — enforce case here)
                if (segments[i] != "dlc.rpf")
                    continue;

                bool hasDlcpacks = segments.Any(s => s.Equals("dlcpacks", StringComparison.OrdinalIgnoreCase));
                bool hasParent = i > 0;

                if (hasDlcpacks || hasParent)
                    return true;
            }
        }
        return false;
    }

    private static bool HasReplaceVehicleSignature(IReadOnlyList<BundleFile> files)
    {
        bool hasDlcRpf = files.Any(f =>
            Path.GetFileName(f.RelativePath).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));

        if (hasDlcRpf)
            return false;

        bool hasYft = files.Any(f => f.Extension == ".yft");
        bool hasYtd = files.Any(f => f.Extension == ".ytd");

        return hasYft && hasYtd;
    }

    private static bool HasElsSignature(IReadOnlyList<BundleFile> files)
    {
        return files.Any(f =>
            f.Extension == ".xml" &&
            f.RelativePath.Contains("ELS/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasWeaponAddonSignature(IReadOnlyList<BundleFile> files)
    {
        bool hasDlcRpf = files.Any(f =>
            Path.GetFileName(f.RelativePath).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));

        if (!hasDlcRpf)
            return false;

        return files.Any(f =>
        {
            if (f.Extension != ".meta")
                return false;
            var fileName = Path.GetFileName(f.RelativePath);
            return fileName.StartsWith("weapon", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("weapons.meta", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static BundleType? ParseBundleType(string raw) =>
        raw.ToLowerInvariant() switch
        {
            "vehicle_addon"  => BundleType.VehicleAddon,
            "vehicle_replace" => BundleType.VehicleReplace,
            "els"            => BundleType.Els,
            "rph_plugin"     => BundleType.RphPlugin,
            "shvdn_script"   => BundleType.ShvdnScript,
            "siren_pack"     => BundleType.SirenPack,
            "weapon_addon"   => BundleType.WeaponAddon,
            _                => null
        };

    private static ClassificationResult Refuse(string reason) =>
        new() { RefusalReasons = [reason] };
}
