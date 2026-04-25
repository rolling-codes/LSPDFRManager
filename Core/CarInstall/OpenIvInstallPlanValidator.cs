using LSPDFRManager.OpenIv.CarInstall.Models;

namespace LSPDFRManager.OpenIv.CarInstall;

public static class OpenIvInstallPlanValidator
{
    public static void Validate(OpenIvInstallPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        // 1. Plan integrity
        if (plan.Operations == null || plan.Operations.Count == 0)
            throw new InvalidOperationException("Install plan contains no operations.");

        if (!Enum.IsDefined(typeof(CarInstallType), plan.Type))
            throw new InvalidOperationException($"Invalid CarInstallType: {plan.Type}");

        // 2. Path safety (mods/ enforcement)
        foreach (var op in plan.Operations)
        {
            if (string.IsNullOrWhiteSpace(op.SourcePath))
                throw new InvalidOperationException("Source path cannot be empty.");

            if (string.IsNullOrWhiteSpace(op.DestinationPath))
                throw new InvalidOperationException("Destination path cannot be empty.");

            var normalizedDest = op.DestinationPath.Replace("/", @"\").ToLowerInvariant();
            if (!normalizedDest.StartsWith(@"mods\") && normalizedDest != "mods")
                throw new InvalidOperationException(
                    $"Unsafe path detected: '{op.DestinationPath}'. All files must be installed under mods/ directory.");
        }

        // 3. DLC naming safety (AddonDLC only)
        if (plan.Type == CarInstallType.AddonDLC)
        {
            if (string.IsNullOrWhiteSpace(plan.TargetDlcName))
                throw new InvalidOperationException("DLC name is required for AddonDLC installations.");

            var invalidChars = Path.GetInvalidFileNameChars();
            if (plan.TargetDlcName.Any(c => invalidChars.Contains(c)))
                throw new InvalidOperationException(
                    $"DLC name '{plan.TargetDlcName}' contains invalid characters for filesystem.");

            // Reject reserved/problematic names
            var reservedNames = new[] { "patchday", "x64", "update", "mods", "gta", "game" };
            if (reservedNames.Contains(plan.TargetDlcName.ToLowerInvariant()))
                throw new InvalidOperationException(
                    $"DLC name '{plan.TargetDlcName}' is reserved and cannot be used.");
        }

        // 4. XML patch safety (dlclist.xml only, append-only)
        foreach (var patch in plan.XmlPatches)
        {
            if (string.IsNullOrWhiteSpace(patch.FilePath))
                throw new InvalidOperationException("XML patch file path cannot be empty.");

            var normalizedPath = patch.FilePath.Replace("/", @"\").ToLowerInvariant();
            if (!normalizedPath.EndsWith("dlclist.xml"))
                throw new InvalidOperationException(
                    $"Only dlclist.xml patches are allowed. Found: {patch.FilePath}");

            if (string.IsNullOrWhiteSpace(patch.XPath))
                throw new InvalidOperationException("XML patch XPath cannot be empty.");

            if (string.IsNullOrWhiteSpace(patch.Value))
                throw new InvalidOperationException("XML patch value cannot be empty.");
        }

        // 5. Type-content consistency
        if (plan.Type == CarInstallType.ConfigPatch && plan.XmlPatches.Any())
            throw new InvalidOperationException(
                "ConfigPatch type cannot contain XML patches. Use AddonDLC for DLC modifications.");

        if (plan.Type == CarInstallType.ReplaceVehicle && plan.XmlPatches.Any())
            throw new InvalidOperationException(
                "ReplaceVehicle type cannot modify dlclist.xml.");

        if (plan.Type == CarInstallType.AddonDLC && !plan.XmlPatches.Any())
            throw new InvalidOperationException(
                "AddonDLC type must have at least one XML patch (dlclist.xml entry).");
    }
}
