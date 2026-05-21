using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public static class BackupUnitFilter
{
    public static List<BackupUnitDefinition> Filter(
        IEnumerable<BackupUnitDefinition> units,
        string? department,
        string? county,
        EupGender? gender,
        string? category)
    {
        return units
            .Where(u => IsAny(department) ||
                        u.Agency.Equals(department!, StringComparison.OrdinalIgnoreCase))
            .Where(u => IsAny(county) ||
                        u.Region.Equals(county!, StringComparison.OrdinalIgnoreCase) ||
                        MetaMatch(u, "County", county!))
            .Where(u => gender is null || gender == EupGender.Any ||
                        // Filter only has unit metadata, not source folder path. We intentionally
                        // pass an empty folderPath until file-origin context is plumbed through.
                        EupInferenceHelper.InferGender(u.PedModel, u.DisplayName, "") == gender ||
                        EupInferenceHelper.InferGender(u.PedModel, u.DisplayName, "") == EupGender.Unknown)
            .Where(u => IsAny(category) ||
                        u.UnitType.Equals(category!, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsAny(string? value) =>
        string.IsNullOrEmpty(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase);

    private static bool MetaMatch(BackupUnitDefinition u, string key, string value) =>
        u.Metadata.TryGetValue(key, out var v) &&
        v.Equals(value, StringComparison.OrdinalIgnoreCase);
}
