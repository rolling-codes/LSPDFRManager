using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class InstalledModFileService
{
    public void SetEnabled(InstalledMod mod, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(mod);

        foreach (var file in mod.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                ToggleFile(file, enabled);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Toggle '{file}' failed: {ex.Message}");
            }
        }

        mod.IsEnabled = enabled;
    }

    public void Uninstall(InstalledMod mod) => Uninstall(mod, []);

    public void Uninstall(InstalledMod mod, IEnumerable<InstalledMod> installedMods)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(installedMods);

        if (!mod.IsEnabled)
            SetEnabled(mod, true);

        var sharedFiles = installedMods
            .Where(other => other.Id != mod.Id)
            .SelectMany(other => other.InstalledFiles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deletedFileDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in mod.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (sharedFiles.Contains(file))
            {
                AppLogger.Warning($"Skipping shared file during uninstall: {file}");
                continue;
            }

            DeleteIfExists(file);
            DeleteIfExists(GetDisabledPath(file));

            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
                deletedFileDirs.Add(dir);
        }

        if (!string.IsNullOrEmpty(mod.InstallPath))
            PruneEmptyDirectories(deletedFileDirs, mod.InstallPath);

        if (mod.Type == ModType.VehicleDlc && !string.IsNullOrWhiteSpace(mod.DlcPackName) &&
            !IsDlcPackUsedByOtherMod(mod, installedMods))
        {
            DlcListService.RemoveEntry(mod.DlcPackName);
        }
    }

    private static void PruneEmptyDirectories(IEnumerable<string> candidates, string installRoot)
    {
        var root = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Work deepest-first so parent dirs can also be pruned once children are removed
        var sorted = candidates
            .Select(d => Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Where(d => d.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !d.Equals(root, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Length)
            .ToList();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in sorted)
        {
            var current = dir;
            while (!string.IsNullOrEmpty(current) &&
                   current.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                   !current.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                   visited.Add(current))
            {
                try
                {
                    if (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
                        Directory.Delete(current);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"Prune directory '{current}' failed: {ex.Message}");
                    break;
                }

                current = Path.GetDirectoryName(current) ?? "";
            }
        }
    }

    public List<string> FindConflicts(IEnumerable<InstalledMod> installedMods, InstalledMod candidate)
    {
        var conflicts = new List<string>();
        var otherMods = installedMods.Where(mod => mod.Id != candidate.Id).ToList();

        if (!string.IsNullOrWhiteSpace(candidate.DlcPackName) &&
            otherMods.Any(mod => mod.DlcPackName.Equals(candidate.DlcPackName, StringComparison.OrdinalIgnoreCase)))
        {
            conflicts.Add($"DLC pack name '{candidate.DlcPackName}' is already installed");
        }

        var installedFiles = otherMods
            .SelectMany(mod => mod.InstalledFiles.Select(NormalizeDisabledPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in candidate.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalized = NormalizeDisabledPath(file);
            if (installedFiles.Contains(normalized))
                conflicts.Add($"File conflict: {Path.GetFileName(file)}");
            else if (File.Exists(normalized + ".disabled"))
                conflicts.Add($"Disabled file conflict: {Path.GetFileName(file)}");
        }

        return conflicts;
    }

    private static bool IsDlcPackUsedByOtherMod(InstalledMod mod, IEnumerable<InstalledMod> installedMods)
    {
        return installedMods.Any(other =>
            other.Id != mod.Id &&
            !string.IsNullOrWhiteSpace(other.DlcPackName) &&
            other.DlcPackName.Equals(mod.DlcPackName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ToggleFile(string file, bool enabled)
    {
        var disabledPath = GetDisabledPath(file);

        if (enabled)
        {
            if (File.Exists(disabledPath) && !File.Exists(file))
                File.Move(disabledPath, file);
            return;
        }

        if (File.Exists(file))
            File.Move(file, disabledPath);
    }

    private static string GetDisabledPath(string file) => file + ".disabled";

    private static string NormalizeDisabledPath(string path) =>
        path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path[..^".disabled".Length]
            : path;

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Delete '{path}' failed: {ex.Message}");
        }
    }
}
