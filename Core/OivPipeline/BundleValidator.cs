using System.Collections.ObjectModel;
using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class BundleValidator
{
    // TODO: Populate with verified slot mappings from OpenIV documentation.
    private static readonly IReadOnlyDictionary<string, string> KnownSlotMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public static ValidationResult Validate(
        ClassificationResult classification,
        IReadOnlyList<BundleFile> files,
        BundleManifest? manifest)
    {
        if (!classification.IsClassified)
            return new ValidationResult { Passed = false, RefusalReasons = classification.RefusalReasons };

        var gates = classification.Type switch
        {
            BundleType.VehicleAddon   => ValidateVehicleAddon(files, manifest),
            BundleType.VehicleReplace => ValidateVehicleReplace(files, manifest),
            BundleType.Els            => ValidateEls(files, manifest),
            BundleType.RphPlugin      => ValidateRphPlugin(files, manifest),
            BundleType.ShvdnScript    => ValidateShvdnScript(files, manifest),
            BundleType.SirenPack      => ValidateSirenPack(files, manifest),
            BundleType.WeaponAddon    => ValidateWeaponAddon(files, manifest),
            _                         => new List<ValidationGate>()
        };

        var refusalReasons = gates
            .Where(g => !g.Passed)
            .Select(g => g.Reason ?? g.Name)
            .ToList();

        return new ValidationResult
        {
            Passed = refusalReasons.Count == 0,
            Gates = gates,
            RefusalReasons = refusalReasons
        };
    }

    public static string? KnownSlotMapLookup(string replaceSlot) =>
        KnownSlotMap.TryGetValue(replaceSlot, out var path) ? path : null;

    public static string? DetectDlcPackName(IReadOnlyList<BundleFile> files)
    {
        foreach (var file in files)
        {
            var parts = file.RelativePath.Split('/');
            var dlcRpfIdx = Array.FindIndex(parts, p => p.Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));
            if (dlcRpfIdx <= 0)
                continue;

            var dlcpacksIdx = Array.FindIndex(parts, p => p.Equals("dlcpacks", StringComparison.OrdinalIgnoreCase));
            if (dlcpacksIdx >= 0 && dlcpacksIdx + 1 < dlcRpfIdx)
                return parts[dlcpacksIdx + 1];

            return parts[dlcRpfIdx - 1];
        }

        return null;
    }

    private static List<ValidationGate> ValidateVehicleAddon(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasDlcRpf = files.Any(f => Path.GetFileName(f.RelativePath).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_dlc_rpf", hasDlcRpf,
            hasDlcRpf ? null : "vehicle_addon must contain a dlc.rpf file in a top-level DLC folder."));

        var dlcPackName = manifest?.DlcPackName ?? DetectDlcPackName(files);
        var hasDlcPackName = dlcPackName is not null;
        gates.Add(new ValidationGate("has_dlc_pack_name", hasDlcPackName,
            hasDlcPackName ? null : "dlcPackName is required. Add 'dlcPackName' to manifest.json."));

        if (dlcPackName is not null)
        {
            var folderMatches = manifest?.SourceFolder is not null || DlcFolderMatchesDlcPackName(files, dlcPackName);
            gates.Add(new ValidationGate("dlc_folder_matches_pack_name", folderMatches,
                folderMatches ? null : $"DLC folder name does not match dlcPackName '{dlcPackName}'. Set 'sourceFolder' in manifest.json to allow a different source folder name."));
        }

        var noMixed = !HasMixedAddonReplace(files);
        gates.Add(new ValidationGate("no_mixed_addon_replace", noMixed,
            noMixed ? null : "Bundle mixes add-on and replace assets. Provide explicit manifest instructions."));

        return gates;
    }

    private static List<ValidationGate> ValidateVehicleReplace(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasReplaceSlotOrPath = !string.IsNullOrEmpty(manifest?.ReplaceSlot) || !string.IsNullOrEmpty(manifest?.TargetArchivePath);
        gates.Add(new ValidationGate("has_replace_slot_or_target_path", hasReplaceSlotOrPath,
            hasReplaceSlotOrPath ? null : "'replaceSlot' or 'targetArchivePath' is required for vehicle_replace."));

        var hasYft = files.Any(f => f.Extension.Equals(".yft", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_yft", hasYft,
            hasYft ? null : "vehicle_replace must include a .yft model file."));

        var hasYtd = files.Any(f => f.Extension.Equals(".ytd", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_ytd", hasYtd,
            hasYtd ? null : "vehicle_replace must include a .ytd texture file."));

        var targetDeterminable = !string.IsNullOrWhiteSpace(manifest?.TargetArchivePath) ||
            (!string.IsNullOrWhiteSpace(manifest?.ReplaceSlot) && KnownSlotMap.ContainsKey(manifest.ReplaceSlot));
        gates.Add(new ValidationGate("target_path_determinable", targetDeterminable,
            targetDeterminable ? null : "Target archive path cannot be determined. Set 'targetArchivePath' in manifest.json or use a slot from the verified slot map."));

        return gates;
    }

    private static List<ValidationGate> ValidateEls(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasXmlConfig = files.Any(f => f.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_xml_config", hasXmlConfig,
            hasXmlConfig ? null : "ELS package must contain .xml configuration files."));

        var hasElsAsi = files.Any(f => Path.GetFileName(f.RelativePath).Equals("ELS.asi", StringComparison.OrdinalIgnoreCase));
        if (hasElsAsi)
        {
            var hasAdvancedHookV = files.Any(f => Path.GetFileName(f.RelativePath).Equals("AdvancedHookV.dll", StringComparison.OrdinalIgnoreCase));
            var elsAsiDepsOk = hasAdvancedHookV || manifest?.ConfigOnly == true;
            gates.Add(new ValidationGate("els_asi_deps", elsAsiDepsOk,
                elsAsiDepsOk ? null : "ELS.asi requires AdvancedHookV.dll. Include it or set 'configOnly: true' in manifest.json."));

            var hasElsVehicleConfigs = files.Any(f =>
                f.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
                f.RelativePath.Contains("ELS/", StringComparison.OrdinalIgnoreCase));
            if (hasElsVehicleConfigs)
            {
                var noMixedCoreConfig = manifest is not null;
                gates.Add(new ValidationGate("els_no_mixed_core_config", noMixedCoreConfig,
                    noMixedCoreConfig ? null : "Bundle mixes ELS core (ELS.asi) and vehicle configs. Provide manifest.json to validate this combination."));
            }
        }

        return gates;
    }

    private static List<ValidationGate> ValidateRphPlugin(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasManifest = manifest is not null;
        gates.Add(new ValidationGate("has_manifest", hasManifest,
            hasManifest ? null : "rph_plugin requires a manifest.json declaration."));

        var hasDll = files.Any(f => f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_dll", hasDll,
            hasDll ? null : "rph_plugin must contain .dll plugin files."));

        return gates;
    }

    private static List<ValidationGate> ValidateShvdnScript(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasManifest = manifest is not null;
        gates.Add(new ValidationGate("has_manifest", hasManifest,
            hasManifest ? null : "shvdn_script requires a manifest.json declaration."));

        var hasScriptFile = files.Any(f =>
            f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_script_file", hasScriptFile,
            hasScriptFile ? null : "shvdn_script must contain .dll or .cs script files."));

        return gates;
    }

    private static List<ValidationGate> ValidateSirenPack(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasManifest = manifest is not null;
        gates.Add(new ValidationGate("has_manifest", hasManifest,
            hasManifest ? null : "siren_pack requires a manifest.json declaration."));

        return gates;
    }

    private static List<ValidationGate> ValidateWeaponAddon(IReadOnlyList<BundleFile> files, BundleManifest? manifest)
    {
        var gates = new List<ValidationGate>();

        var hasDlcRpf = files.Any(f => Path.GetFileName(f.RelativePath).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase));
        gates.Add(new ValidationGate("has_dlc_rpf", hasDlcRpf,
            hasDlcRpf ? null : "weapon_addon requires a clean DLC layout with dlc.rpf."));

        var hasManifest = manifest is not null;
        gates.Add(new ValidationGate("has_manifest", hasManifest,
            hasManifest ? null : "weapon_addon requires a manifest.json."));

        var hasDlcPackName = manifest?.DlcPackName is not null;
        gates.Add(new ValidationGate("has_dlc_pack_name", hasDlcPackName,
            hasDlcPackName ? null : "'dlcPackName' is required for weapon_addon."));

        var hasLooseMeta = files.Any(f =>
            f.Extension.Equals(".meta", StringComparison.OrdinalIgnoreCase) &&
            !f.RelativePath.Contains("dlc.rpf", StringComparison.OrdinalIgnoreCase));
        if (hasLooseMeta)
        {
            gates.Add(new ValidationGate("no_loose_weapon_meta", false,
                "Loose weapon meta files detected outside dlc.rpf. Provide explicit merge instructions in manifest.json."));
        }

        return gates;
    }

    private static bool DlcFolderMatchesDlcPackName(IReadOnlyList<BundleFile> files, string dlcPackName)
    {
        var detected = DetectDlcPackName(files);
        return detected?.Equals(dlcPackName, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool HasMixedAddonReplace(IReadOnlyList<BundleFile> files)
    {
        // Collect the parent directories of all dlc.rpf files (the DLC pack folders)
        var dlcPackDirs = files
            .Where(f => Path.GetFileName(f.RelativePath).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var dir = Path.GetDirectoryName(f.RelativePath.Replace('\\', '/'))?.Replace('\\', '/') ?? "";
                return dir.TrimEnd('/');
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (dlcPackDirs.Count == 0) return false;

        // A .yft/.ytd is "loose" (replace indicator) only if it is NOT inside any DLC pack folder
        return files.Any(f =>
            (f.Extension.Equals(".yft", StringComparison.OrdinalIgnoreCase) ||
             f.Extension.Equals(".ytd", StringComparison.OrdinalIgnoreCase)) &&
            !dlcPackDirs.Any(dir =>
                string.IsNullOrEmpty(dir)
                    ? false
                    : f.RelativePath.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase) ||
                      f.RelativePath.Equals(dir, StringComparison.OrdinalIgnoreCase)));
    }
}
