using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class InstalledModFileService
{
    public void SetEnabled(InstalledMod mod, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(mod);

        var failed = new List<string>();
        var completedRenames = new List<(string from, string to)>();
        foreach (var file in mod.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsWithinGtaPath(file))
            {
                AppLogger.Warning($"[InstalledModFileService] Skipping toggle on path outside GTA directory: {file}");
                failed.Add(file);
                continue;
            }

            try
            {
                var rename = ToggleFile(file, enabled);
                if (rename is not null)
                    completedRenames.Add(rename.Value);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Toggle '{file}' failed: {ex.Message}");
                failed.Add(file);
            }
        }

        if (failed.Count == 0)
            mod.IsEnabled = enabled;
        else
        {
            RollbackRenames(completedRenames);
            AppLogger.Warning($"SetEnabled({enabled}) incomplete for '{mod.Name}': {failed.Count} file(s) could not be toggled — library state not updated.");
        }
    }

    public ModUninstallResult Uninstall(InstalledMod mod) => Uninstall(mod, []);

    public ModUninstallResult Uninstall(InstalledMod mod, IEnumerable<InstalledMod> installedMods)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(installedMods);

        var result = new ModUninstallResult();

        var sharedFiles = installedMods
            .Where(other => other.Id != mod.Id)
            .SelectMany(other => other.InstalledFiles)
            .Select(NormalizeDisabledPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deletedFileDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in mod.InstalledFiles.Select(NormalizeDisabledPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsWithinGtaPath(file))
            {
                var msg = $"[InstalledModFileService] Rejected uninstall path outside GTA directory: {file}";
                result.Errors.Add(msg);
                AppLogger.Warning(msg);
                continue;
            }

            if (sharedFiles.Contains(file))
            {
                result.SkippedSharedFiles.Add(file);
                AppLogger.Warning($"Skipping shared file during uninstall: {file}");
                continue;
            }

            DeleteRecordedFile(file, result);

            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
                deletedFileDirs.Add(dir);
        }

        if (!string.IsNullOrEmpty(mod.InstallPath))
            PruneEmptyDirectories(deletedFileDirs, mod.InstallPath, result);

        if (mod.Type == ModType.VehicleDlc && !string.IsNullOrWhiteSpace(mod.DlcPackName) &&
            !IsDlcPackUsedByOtherMod(mod, installedMods))
        {
            try
            {
                DlcListService.RemoveEntry(mod.DlcPackName);
            }
            catch (Exception ex)
            {
                var message = $"Remove DLC list entry '{mod.DlcPackName}' failed: {ex.Message}";
                result.Errors.Add(message);
                AppLogger.Warning(message);
            }
        }

        return result;
    }

    private static void PruneEmptyDirectories(IEnumerable<string> candidates, string installRoot, ModUninstallResult result)
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
                    var message = $"Prune directory '{current}' failed: {ex.Message}";
                    result.Errors.Add(message);
                    AppLogger.Warning(message);
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

    private static (string from, string to)? ToggleFile(string file, bool enabled)
    {
        var disabledPath = GetDisabledPath(file);

        if (enabled)
        {
            if (File.Exists(disabledPath) && !File.Exists(file))
            {
                File.Move(disabledPath, file);
                return (disabledPath, file);
            }
            return null;
        }

        if (File.Exists(file))
        {
            File.Move(file, disabledPath);
            return (file, disabledPath);
        }

        return null;
    }

    private static void RollbackRenames(List<(string from, string to)> completedRenames)
    {
        foreach (var (from, to) in completedRenames.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(to) && !File.Exists(from))
                    File.Move(to, from);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Rollback toggle '{to}' -> '{from}' failed: {ex.Message}");
            }
        }
    }

    private static bool IsWithinGtaPath(string fullPath)
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrWhiteSpace(gtaPath)) return false;
        try
        {
            var relative = Path.GetRelativePath(gtaPath, fullPath);
            PathSafety.GetSafePath(gtaPath, relative);
            return true;
        }
        catch { return false; }
    }

    private static string GetDisabledPath(string file) => file + ".disabled";

    private static string NormalizeDisabledPath(string path) =>
        path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path[..^".disabled".Length]
            : path;

    private static void DeleteRecordedFile(string file, ModUninstallResult result)
    {
        var paths = new[] { file, GetDisabledPath(file) }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var foundAny = false;

        foreach (var path in paths)
        {
            if (!File.Exists(path))
                continue;

            foundAny = true;
            DeleteExisting(path, result);
        }

        if (!foundAny)
            result.MissingFiles.Add(file);
    }

    private static void DeleteExisting(string path, ModUninstallResult result)
    {
        try
        {
            File.Delete(path);
            result.DeletedFiles.Add(path);
        }
        catch (Exception ex)
        {
            result.AddDeleteFailure(path);
            AppLogger.Warning($"Delete '{path}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true when the mod has a non-empty file list but none of its files exist
    /// on disk (neither the active path nor the .disabled variant). These are ghost
    /// entries left behind after files were removed outside of LSPDFRManager.
    /// </summary>
    public static bool IsOrphaned(InstalledMod mod) =>
        mod.InstalledFiles.Count > 0 &&
        !mod.InstalledFiles.Any(f => File.Exists(f) || File.Exists(f + ".disabled"));
}
