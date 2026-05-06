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

        foreach (var file in mod.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (sharedFiles.Contains(file))
            {
                AppLogger.Warning($"Skipping shared file during uninstall: {file}");
                continue;
            }

            DeleteIfExists(file);
            DeleteIfExists(GetDisabledPath(file));
        }

        if (mod.Type == ModType.VehicleDlc && !string.IsNullOrWhiteSpace(mod.DlcPackName) &&
            !IsDlcPackUsedByOtherMod(mod, installedMods))
        {
            DlcListService.RemoveEntry(mod.DlcPackName);
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
            .SelectMany(mod => mod.InstalledFiles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in candidate.InstalledFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (installedFiles.Contains(file))
                conflicts.Add($"File conflict: {Path.GetFileName(file)}");
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
