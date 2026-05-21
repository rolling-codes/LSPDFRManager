using System.Text.Json;
using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class ManifestReader
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "vehicle_addon", "vehicle_replace", "els", "rph_plugin",
        "shvdn_script", "siren_pack", "weapon_addon"
    };

    public static ManifestReadResult Read(IReadOnlyList<BundleFile> files, string bundleRoot)
    {
        var manifestFile = files.FirstOrDefault(f =>
            f.RelativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
            f.RelativePath.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase));

        if (manifestFile is null)
            return new ManifestReadResult();

        var normalizedRelativePath = manifestFile.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        var bundleRootFull = Path.GetFullPath(bundleRoot);
        var bundleRootPrefix = bundleRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fsPath = Path.GetFullPath(Path.Combine(bundleRootFull, normalizedRelativePath));
        if (!fsPath.StartsWith(bundleRootPrefix, StringComparison.OrdinalIgnoreCase))
            return new ManifestReadResult { ValidationErrors = ["manifest.json read error: path escapes bundle root."] };

        string json;
        try
        {
            json = File.ReadAllText(fsPath);
        }
        catch (Exception ex)
        {
            return new ManifestReadResult { ValidationErrors = [$"manifest.json read error: {ex.Message}"] };
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            return new ManifestReadResult { ValidationErrors = [$"manifest.json parse error: {ex.Message}"] };
        }

        using (doc)
        {
            var root = doc.RootElement;
            var errors = new List<string>();

            var typeValue = GetString(root, "type");
            if (typeValue is null || !ValidTypes.Contains(typeValue))
            {
                errors.Add(typeValue is null
                    ? "manifest.json: 'type' field is required."
                    : $"manifest.json: 'type' value '{typeValue}' is not a recognized bundle type.");
                return new ManifestReadResult { ValidationErrors = errors };
            }

            if (typeValue.Equals("vehicle_addon", StringComparison.OrdinalIgnoreCase) ||
                typeValue.Equals("weapon_addon", StringComparison.OrdinalIgnoreCase))
            {
                var dlc = GetString(root, "dlcPackName");
                if (string.IsNullOrEmpty(dlc))
                    errors.Add($"manifest.json: 'dlcPackName' is required for type '{typeValue}'.");
            }

            if (typeValue.Equals("vehicle_replace", StringComparison.OrdinalIgnoreCase))
            {
                var slot = GetString(root, "replaceSlot");
                var targetPath = GetString(root, "targetArchivePath");
                if (string.IsNullOrEmpty(slot) && string.IsNullOrEmpty(targetPath))
                    errors.Add("manifest.json: 'replaceSlot' or 'targetArchivePath' is required for type 'vehicle_replace'.");
            }

            if (errors.Count > 0)
                return new ManifestReadResult { ValidationErrors = errors };

            var manifest = new BundleManifest
            {
                Type = typeValue,
                PackageName = GetString(root, "packageName"),
                DlcPackName = GetString(root, "dlcPackName"),
                ReplaceSlot = GetString(root, "replaceSlot"),
                SourceFolder = GetString(root, "sourceFolder"),
                ConfigOnly = GetBool(root, "configOnly"),
                Dependencies = GetStringArray(root, "dependencies"),
                TargetArchivePath = GetString(root, "targetArchivePath")
            };

            return new ManifestReadResult { Manifest = manifest };
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static bool GetBool(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var result = new List<string>();
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is string s)
                    result.Add(s);
            }
            return result.AsReadOnly();
        }
        return [];
    }
}
