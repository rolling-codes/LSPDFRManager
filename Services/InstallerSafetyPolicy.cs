using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public static class InstallerSafetyPolicy
{
    private static readonly HashSet<string> StopThePedAliases =
    [
        "stoptheped",
        "stop the ped",
        "stoptheped.dll",
        "plugins/lspdfr/stoptheped.dll",
    ];

    private static readonly HashSet<string> UltimateBackupAliases =
    [
        "ultimatebackup",
        "ultimate backup",
        "ultimatebackup.dll",
        "plugins/lspdfr/ultimatebackup.dll",
    ];

    private static readonly HashSet<string> SharedDependencyFiles =
    [
        "newtonsoft.json.dll",
        "lemonui.shvdn3.dll",
        "ragenativeui.dll",
        "nativeui.dll",
        "scripthookvdotnet.asi",
        "scripthookv.dll",
    ];

    private static readonly HashSet<string> ConfigExtensions =
    [
        ".ini", ".xml", ".json", ".cfg", ".meta", ".dat", ".ytd", ".ytf", ".ydd", ".ymt",
    ];

    public static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('\\', '/')
            .TrimStart('/');
    }

    public static bool IsStopThePedAlias(string value)
    {
        var normalized = NormalizeRelativePath(value).ToLowerInvariant();
        return StopThePedAliases.Contains(normalized);
    }

    public static bool IsUltimateBackupAlias(string value)
    {
        var normalized = NormalizeRelativePath(value).ToLowerInvariant();
        return UltimateBackupAliases.Contains(normalized);
    }

    public static bool IsStopThePedFile(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath).ToLowerInvariant();
        return normalized.Contains("stoptheped") || StopThePedAliases.Contains(normalized);
    }

    public static bool IsUltimateBackupFile(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath).ToLowerInvariant();
        return normalized.Contains("ultimatebackup")
            || normalized.Contains("ultimate backup")
            || UltimateBackupAliases.Contains(normalized);
    }

    public static bool IsSharedDependency(string relativePath)
    {
        var fileName = Path.GetFileName(NormalizeRelativePath(relativePath)).ToLowerInvariant();
        return SharedDependencyFiles.Contains(fileName);
    }

    public static bool IsPluginBinary(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath).ToLowerInvariant();
        if (!normalized.StartsWith("plugins/lspdfr/", StringComparison.OrdinalIgnoreCase))
            return false;

        return normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".asi", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKnownBackupConfig(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath).ToLowerInvariant();
        var fileName = Path.GetFileName(normalized);

        if (normalized.Contains("plugins/lspdfr/") &&
            (normalized.EndsWith(".ini") || normalized.EndsWith(".xml")))
            return true;

        if (normalized.StartsWith("lspdfr/data/", StringComparison.OrdinalIgnoreCase)
            && (normalized.EndsWith(".xml") || normalized.EndsWith(".ini")))
            return true;

        if (normalized.Contains("/custom/")
            && (normalized.EndsWith(".xml") || normalized.EndsWith(".ini")))
            return true;

        if (normalized.StartsWith("els/", StringComparison.OrdinalIgnoreCase) && normalized.EndsWith(".xml"))
            return true;

        if (fileName.Equals("els.ini", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.Equals("backups.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("backup.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("agency.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("regions.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("customregions.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("units.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("defaultregions.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("special", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsStopThePedFile(relativePath) || IsUltimateBackupFile(relativePath);
    }

    public static bool IsSensitiveOverwriteTarget(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath).ToLowerInvariant();
        var extension = Path.GetExtension(normalized).ToLowerInvariant();

        if (normalized.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.StartsWith("plugins/lspdfr/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("lspdfr/data/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.StartsWith("els/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (extension is ".dll" or ".asi")
            return true;

        return ConfigExtensions.Contains(extension) || IsKnownBackupConfig(relativePath);
    }

    public static InstallOverwriteRisk ClassifyOverwriteRisk(string relativePath, bool destinationExists)
    {
        if (!destinationExists)
            return InstallOverwriteRisk.None;

        if (IsKnownBackupConfig(relativePath))
            return InstallOverwriteRisk.Critical;

        if (IsSensitiveOverwriteTarget(relativePath))
            return InstallOverwriteRisk.High;

        return InstallOverwriteRisk.Medium;
    }

    public static InstallConflictAction DefaultConflictAction(string relativePath, bool destinationExists)
    {
        if (!destinationExists)
            return InstallConflictAction.BackupAndReplace;

        if (IsKnownBackupConfig(relativePath))
            return InstallConflictAction.RenameIncoming;

        if (IsSensitiveOverwriteTarget(relativePath))
            return InstallConflictAction.KeepExisting;

        return InstallConflictAction.BackupAndReplace;
    }

    public static string? DefaultOverwriteReason(string relativePath, bool destinationExists)
    {
        if (!destinationExists)
            return null;

        if (IsKnownBackupConfig(relativePath))
            return "Existing backup/config file detected. Preserve current file by default.";

        if (IsPluginBinary(relativePath))
            return "Plugin binary already exists. Replacement requires explicit user choice.";

        if (IsSensitiveOverwriteTarget(relativePath))
            return "Sensitive mod/config path detected. Defaulting to preserve existing file.";

        return "Destination exists and will be overwritten if replacement is selected.";
    }

    public static string? DetectPluginFamily(string relativePath)
    {
        if (IsStopThePedFile(relativePath)) return "Stop The Ped";
        if (IsUltimateBackupFile(relativePath)) return "Ultimate Backup";
        if (IsSharedDependency(relativePath)) return "Shared Dependency";
        return null;
    }

    public static int GetInstallOrderPriority(string relativePath, bool hasStopThePed, bool hasUltimateBackup)
    {
        if (IsSharedDependency(relativePath))
            return 10;

        if (hasStopThePed && hasUltimateBackup)
        {
            if (IsStopThePedFile(relativePath) && IsPluginBinary(relativePath))
                return 20;

            if (IsStopThePedFile(relativePath))
                return 30;

            if (IsUltimateBackupFile(relativePath) && IsPluginBinary(relativePath))
                return 40;

            if (IsUltimateBackupFile(relativePath))
                return 50;
        }

        return 100;
    }

    public static string? BuildDependencyReason(string relativePath, bool hasStopThePed, bool hasUltimateBackup)
    {
        if (!(hasStopThePed && hasUltimateBackup))
            return null;

        if (IsSharedDependency(relativePath))
            return "Shared dependency scheduled before plugin DLLs.";

        if (IsStopThePedFile(relativePath))
            return "Stop The Ped must install before Ultimate Backup for transport/coroner integration.";

        if (IsUltimateBackupFile(relativePath))
            return "Ultimate Backup is scheduled after Stop The Ped integration files.";

        return null;
    }

    public static string GetUltimateBackupMissingStpWarning()
    {
        return "Ultimate Backup detected. Some Ultimate Backup features, including Police Transport and Coroner-style units, require Stop The Ped. Install Stop The Ped first or ensure it is already installed.";
    }

    public static bool ReferencesTransportOrCoroner(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lowered = content.ToLowerInvariant();
        return lowered.Contains("transport")
            || lowered.Contains("coroner")
            || lowered.Contains("prisoner")
            || lowered.Contains("buddy");
    }

    public static string BuildIncomingRenamePath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);

        var candidate = Path.Combine(directory, fileNameWithoutExt + ".incoming" + extension);
        if (!File.Exists(candidate))
            return candidate;

        var index = 1;
        while (true)
        {
            var numbered = Path.Combine(directory, fileNameWithoutExt + $".incoming.{index}" + extension);
            if (!File.Exists(numbered))
                return numbered;

            index++;
        }
    }
}
