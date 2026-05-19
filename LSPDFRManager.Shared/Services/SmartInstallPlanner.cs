using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using SharpCompress.Archives;

namespace LSPDFRManager.Services;

public class SmartInstallPlanner
{
    private readonly ModDetector                 _detector     = new();
    private readonly IModTypeDetectionService    _typeDetector = new ModTypeDetectionService();
    private readonly IDependencyDetectionService _depDetector  = new DependencyDetectionService();
    private readonly IDependencyProbeService     _probeService = new DependencyProbeService();

    private sealed record SourceEntry(string RelativePath, Func<Stream> OpenStream, int SourceIndex);

    public InstallPlan BuildPlan(string archivePath, bool dryRun = false)
    {
        var modInfo = _detector.Detect(archivePath);
        var gtaPath = AppConfig.Instance.GtaPath;
        var entries = new List<InstallPlanEntry>();
        var warnings = new List<string>();
        var blockingIssues = new List<string>();
        OivPackage? oivMetadata = null;
        var orderReasons = new List<string>();
        string? readmeContent = null;

        var sourceEntries = LoadSourceEntries(archivePath, warnings);

        var entryPaths    = sourceEntries.Select(e => e.RelativePath).ToList();
        var modTypeResult = _typeDetector.Detect(entryPaths, Path.GetFileName(archivePath));
        var depResult     = _depDetector.Detect(modTypeResult);
        var probeResult   = _probeService.Probe(gtaPath, depResult);
        foreach (var dep in depResult.Warnings)
            warnings.Add($"Dependency: {dep.Name} — {dep.Reason}");

        if (modTypeResult.PrimaryType == ModType.OivPackage)
        {
            blockingIssues.Add(InstallerSafetyPolicy.OivPrimaryBlockMessage);
            var asmEntry = sourceEntries.FirstOrDefault(e =>
                string.Equals(e.RelativePath, "assembly.xml", StringComparison.OrdinalIgnoreCase) ||
                (e.RelativePath.EndsWith("/assembly.xml", StringComparison.OrdinalIgnoreCase) &&
                 e.RelativePath.Count(c => c == '/') == 1));
            if (asmEntry is not null)
            {
                try
                {
                    using var asmStream = asmEntry.OpenStream();
                    oivMetadata = OivService.ParseFromStream(asmStream, archivePath);
                    if (!oivMetadata.IsValid)
                    {
                        warnings.Add($"Could not parse OIV manifest: {oivMetadata.ValidationError}");
                        oivMetadata = null;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"[PLANNER] Failed to open OIV manifest stream in {archivePath}: {ex}");
                    warnings.Add($"Could not read OIV manifest: {ex.Message}");
                }
            }
        }
        else if (modTypeResult.SecondaryTypes.Any(t => t.Type == ModType.OivPackage))
            warnings.Add(InstallerSafetyPolicy.OivSecondaryWarningMessage);

        var hasStopThePedInBatch = sourceEntries.Any(e => InstallerSafetyPolicy.IsStopThePedFile(e.RelativePath));
        var hasUltimateBackupInBatch = sourceEntries.Any(e => InstallerSafetyPolicy.IsUltimateBackupFile(e.RelativePath));
        var stopThePedInstalled = IsStopThePedInstalled(gtaPath);

        var sortedEntries = sourceEntries
            .OrderBy(e => InstallerSafetyPolicy.GetInstallOrderPriority(e.RelativePath, hasStopThePedInBatch, hasUltimateBackupInBatch))
            .ThenBy(e => e.SourceIndex)
            .ToList();

        foreach (var source in sortedEntries)
        {
            var entryPath = InstallerSafetyPolicy.NormalizeRelativePath(source.RelativePath);
            string targetPath;
            bool safePath = true;

            try
            {
                targetPath = PathSafety.GetSafePath(gtaPath, entryPath);
            }
            catch
            {
                targetPath = string.Empty;
                safePath = false;
            }

            var destinationExists = safePath && File.Exists(targetPath);
            var fileName = Path.GetFileName(entryPath).ToLowerInvariant();
            var overwriteRisk = InstallerSafetyPolicy.ClassifyOverwriteRisk(entryPath, destinationExists);
            var plannedAction = InstallerSafetyPolicy.DefaultConflictAction(entryPath, destinationExists);
            var overwriteReason = InstallerSafetyPolicy.DefaultOverwriteReason(entryPath, destinationExists);
            var dependencyReason = InstallerSafetyPolicy.BuildDependencyReason(
                entryPath,
                hasStopThePedInBatch,
                hasUltimateBackupInBatch);

            var risk = InstallRisk.Safe;
            if (!safePath)
            {
                risk = InstallRisk.Incompatible;
                warnings.Add($"Suspicious path: {entryPath}");
                plannedAction = InstallConflictAction.Skip;
            }
            else if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                risk = InstallRisk.Suspicious;
                warnings.Add($"Archive contains executable: {fileName}");
            }
            else if (destinationExists)
            {
                risk = InstallRisk.Overwrite;
            }

            string? renamedPath = null;
            if (destinationExists && plannedAction == InstallConflictAction.RenameIncoming)
                renamedPath = InstallerSafetyPolicy.BuildIncomingRenamePath(targetPath);

            if (fileName is "readme.txt" or "readme.md" or "install.txt")
            {
                try { readmeContent = ReadToEnd(source.OpenStream()); }
                catch { }
            }

            if (destinationExists && overwriteRisk >= InstallOverwriteRisk.High)
                warnings.Add($"Overwrite conflict: {entryPath} -> {targetPath}");

            if (dependencyReason is not null)
                orderReasons.Add(dependencyReason);

            if (!stopThePedInstalled && !hasStopThePedInBatch && InstallerSafetyPolicy.IsUltimateBackupFile(entryPath))
            {
                if (CanReadAsText(entryPath) && TryReadText(source.OpenStream, out var content) &&
                    InstallerSafetyPolicy.ReferencesTransportOrCoroner(content))
                {
                    blockingIssues.Add("Ultimate Backup config references transport/coroner behavior that may require Stop The Ped. Confirm before replace/apply.");
                }
            }

            entries.Add(new InstallPlanEntry
            {
                ArchivePath = entryPath,
                TargetPath = targetPath,
                DestinationExists = destinationExists,
                Risk = risk,
                OverwriteRisk = overwriteRisk,
                PlannedAction = plannedAction,
                RenamedTargetPath = renamedPath,
                DetectedPlugin = InstallerSafetyPolicy.DetectPluginFamily(entryPath),
                DependencyReason = dependencyReason,
                OverwriteReason = overwriteReason,
                RequiresExplicitConfirmation = destinationExists && plannedAction == InstallConflictAction.BackupAndReplace,
            });
        }

        if (entries.Any(e => e.WillOverwrite))
            warnings.Add($"{entries.Count(e => e.WillOverwrite)} file(s) already exist at destination.");

        if (hasUltimateBackupInBatch && !(hasStopThePedInBatch || stopThePedInstalled))
            warnings.Add(InstallerSafetyPolicy.GetUltimateBackupMissingStpWarning());

        var installOrder = BuildInstallOrder(sortedEntries, hasStopThePedInBatch, hasUltimateBackupInBatch);

        return new InstallPlan
        {
            ArchiveSource = archivePath,
            DetectedType = modInfo.Type,
            Confidence = modInfo.Confidence,
            ModTypeResult = modTypeResult,
            Entries = entries,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            BlockingIssues = blockingIssues.Distinct(StringComparer.Ordinal).ToList(),
            ReadmeContent = readmeContent,
            IsDryRun = dryRun,
            InstallOrder = installOrder,
            OrderReasons = orderReasons.Distinct(StringComparer.Ordinal).ToList(),
            RequiresManualConfirmation = blockingIssues.Count > 0,
            ProbeResult = probeResult,
            OivMetadata = oivMetadata,
        };
    }

    private static List<string> BuildInstallOrder(
        IReadOnlyList<SourceEntry> sortedEntries,
        bool hasStopThePed,
        bool hasUltimateBackup)
    {
        var order = new List<string>();

        if (sortedEntries.Any(e => InstallerSafetyPolicy.IsSharedDependency(e.RelativePath)))
            order.Add("Shared Dependencies");

        if (hasStopThePed && hasUltimateBackup)
        {
            order.Add("Stop The Ped");
            order.Add("Ultimate Backup");
            return order;
        }

        if (hasStopThePed)
            order.Add("Stop The Ped");
        if (hasUltimateBackup)
            order.Add("Ultimate Backup");

        return order;
    }

    private static bool IsStopThePedInstalled(string gtaPath)
    {
        if (string.IsNullOrWhiteSpace(gtaPath))
            return false;

        var stopThePedDll = Path.Combine(gtaPath, "plugins", "lspdfr", "StopThePed.dll");
        return File.Exists(stopThePedDll);
    }

    private static List<SourceEntry> LoadSourceEntries(string archivePath, List<string> warnings)
    {
        var entries = new List<SourceEntry>();

        try
        {
            if (Directory.Exists(archivePath))
            {
                var files = Directory.GetFiles(archivePath, "*", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    var rel = Path.GetRelativePath(archivePath, file).Replace('\\', '/');
                    entries.Add(new SourceEntry(rel, () => File.OpenRead(file), i));
                }

                return entries;
            }

            using var archive = ArchiveFactory.Open(archivePath);
            var index = 0;
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var rel = (entry.Key ?? string.Empty).Replace('\\', '/');
                byte[] content;
                using (var entryStream = entry.OpenEntryStream())
                using (var buffer = new MemoryStream())
                {
                    entryStream.CopyTo(buffer);
                    content = buffer.ToArray();
                }

                entries.Add(new SourceEntry(rel, () => new MemoryStream(content, writable: false), index++));
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not read archive: {ex.Message}");
        }

        return entries;
    }

    private static bool CanReadAsText(string relativePath)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension is ".ini" or ".cfg" or ".xml" or ".json" or ".txt";
    }

    private static bool TryReadText(Func<Stream> openStream, out string content)
    {
        content = string.Empty;
        try
        {
            content = ReadToEnd(openStream());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadToEnd(Stream stream)
    {
        using (stream)
        using (var reader = new StreamReader(stream))
            return reader.ReadToEnd();
    }
}
