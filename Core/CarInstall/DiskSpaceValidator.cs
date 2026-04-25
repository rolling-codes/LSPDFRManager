using LSPDFRManager.OpenIv.CarInstall.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.OpenIv.CarInstall;

/// <summary>
/// Pre-flight disk space validation before installation.
/// Fails fast if insufficient space available, preventing partial installs.
/// Pure validation layer; no side effects.
/// </summary>
public static class DiskSpaceValidator
{
    private const double SafetyBufferMultiplier = 1.1; // +10% for overhead

    /// <summary>
    /// Validates sufficient disk space exists before execution.
    /// Throws InvalidOperationException if space is insufficient.
    /// </summary>
    public static void EnsureSufficientSpace(
        OpenIvInstallPlan plan,
        IArchive archive,
        string targetRoot)
    {
        if (plan.Operations.Count == 0)
            return;

        var requiredBytes = CalculateRequiredSpace(plan, archive);
        var targetDrive = ExtractDrive(targetRoot);

        if (targetDrive == null)
            throw new InvalidOperationException(
                "Cannot determine target drive from target root");

        try
        {
            var availableSpace = targetDrive.AvailableFreeSpace;

            if (availableSpace < requiredBytes)
            {
                throw new InvalidOperationException(
                    $"Insufficient disk space. Required: {FormatBytes(requiredBytes)}, " +
                    $"Available: {FormatBytes(availableSpace)}");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException(
                $"Cannot access target drive: {targetRoot}");
        }
    }

    private static long CalculateRequiredSpace(
        OpenIvInstallPlan plan,
        IArchive archive)
    {
        long totalSize = 0;

        foreach (var operation in plan.Operations)
        {
            var entry = archive.Entries
                .FirstOrDefault(e => e.Key == operation.SourcePath);

            if (entry != null)
                totalSize += entry.Size;
        }

        // Apply safety buffer for filesystem overhead + patches
        return (long)(totalSize * SafetyBufferMultiplier);
    }

    private static DriveInfo? ExtractDrive(string targetRoot)
    {
        try
        {
            var root = Path.GetPathRoot(targetRoot);
            if (string.IsNullOrEmpty(root))
                return null;

            return new DriveInfo(root);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
