using System.Text.Json;
using System.Text.RegularExpressions;
using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class ManifestReader
{
    private static readonly Regex DlcPackNamePattern = new("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);

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
                else if (!DlcPackNamePattern.IsMatch(dlc))
                    errors.Add("manifest.json: 'dlcPackName' may only contain letters, numbers, underscores, and hyphens.");
            }

            if (typeValue.Equals("vehicle_replace", StringComparison.OrdinalIgnoreCase))
            {
                var slot = GetString(root, "replaceSlot");
                var targetPath = GetString(root, "targetArchivePath");
                if (string.IsNullOrEmpty(slot) && string.IsNullOrEmpty(targetPath))
                    errors.Add("manifest.json: 'replaceSlot' or 'targetArchivePath' is required for type 'vehicle_replace'.");
            }

            var sourceFolder = GetString(root, "sourceFolder");
            if (!string.IsNullOrWhiteSpace(sourceFolder))
            {
                if (!IsSafeRelativeManifestPath(sourceFolder))
                    errors.Add("manifest.json: 'sourceFolder' must be a relative path and cannot contain '..' segments.");
                else if (!BundleContainsFolder(files, sourceFolder))
                    errors.Add($"manifest.json: 'sourceFolder' '{sourceFolder}' was not found in the bundle.");
            }

            var targetArchivePath = GetString(root, "targetArchivePath");
            if (!string.IsNullOrWhiteSpace(targetArchivePath) && !IsSafeRelativeManifestPath(targetArchivePath))
                errors.Add("manifest.json: 'targetArchivePath' must be a relative path and cannot contain '..' segments.");

            if (errors.Count > 0)
                return new ManifestReadResult { ValidationErrors = errors };

            var manifest = new BundleManifest
            {
                Type = typeValue,
                PackageName = GetString(root, "packageName"),
                DlcPackName = GetString(root, "dlcPackName"),
                ReplaceSlot = GetString(root, "replaceSlot"),
                SourceFolder = sourceFolder,
                ConfigOnly = GetBool(root, "configOnly"),
                Dependencies = GetStringArray(root, "dependencies"),
                TargetArchivePath = targetArchivePath
            };

            return new ManifestReadResult { Manifest = manifest };
        }
    }

    private static bool IsSafeRelativeManifestPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (Path.IsPathRooted(path) || normalized.StartsWith('/') || normalized.StartsWith('\\'))
            return false;

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && !segments.Any(s => s.Equals("..", StringComparison.Ordinal));
    }

    private static bool BundleContainsFolder(IReadOnlyList<BundleFile> files, string sourceFolder)
    {
        var prefix = sourceFolder.Replace('\\', '/').Trim().TrimEnd('/') + "/";
        return files.Any(f => f.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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
