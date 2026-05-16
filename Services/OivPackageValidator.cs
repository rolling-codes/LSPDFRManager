using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class OivPackageValidator : IOivPackageValidator
{
    private const long WarningSizeBytes = 4L * 1024 * 1024 * 1024;

    public OivPackagePlan Validate(OivPackagePlan plan)
    {
        var errors   = new List<string>(plan.Errors);
        var warnings = new List<string>(plan.Warnings);

        if (string.IsNullOrWhiteSpace(plan.Name))
            errors.Add("Package name is required.");

        if (plan.Files.Count == 0)
            errors.Add("Package must contain at least one file.");

        // Install path safety: normalize separators then reject traversal, rooted, and absolute paths.
        foreach (var f in plan.Files)
        {
            var ip = f.InstallPath.Replace('\\', '/');

            if (ip.Split('/').Any(seg => seg.Trim() is ".." or "."))
                errors.Add($"Suspicious install path (path traversal): {f.InstallPath}");
            else if (ip.StartsWith('/'))
                errors.Add($"Install path must be relative (starts with '/'): {f.InstallPath}");
            else if (ip.Length >= 2 && ip[1] == ':')
                errors.Add($"Install path must be relative (drive letter): {f.InstallPath}");
            else if (ip.StartsWith("//"))
                errors.Add($"Install path must be relative (UNC path): {f.InstallPath}");
        }

        // Duplicate install paths
        var dupes = plan.Files
            .GroupBy(f => f.InstallPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var d in dupes)
            errors.Add($"Duplicate install path: {d}");

        // Empty install path
        foreach (var f in plan.Files.Where(f => string.IsNullOrWhiteSpace(f.InstallPath)))
            errors.Add($"File has empty install path: {Path.GetFileName(f.SourcePath)}");

        // Missing source files
        foreach (var f in plan.Files.Where(f => !File.Exists(f.SourcePath)))
            errors.Add($"Source file no longer exists: {f.SourcePath}");

        if (plan.TotalBytes > WarningSizeBytes)
            warnings.Add("Total package size exceeds 4 GB — some tools may not support this.");

        return plan with { Errors = errors, Warnings = warnings };
    }
}
