using System.Security.Cryptography;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LSPDFRManager.Services;

/// <summary>
/// Extracts mod files from a <c>.zip</c>, <c>.rar</c>, <c>.7z</c> archive, or
/// plain directory into a target root folder (the GTA V directory).
/// Implements path traversal protection and rollback on failure.
/// </summary>
public static class FileInstaller
{
    private sealed class RollbackFile
    {
        public required string DestinationPath { get; init; }
        public string? BackupPath { get; init; }
        public bool ExistedBeforeInstall => BackupPath is not null;
        public bool WasCommitted { get; set; }
        /// <summary>Set after rollback completes for this entry, preventing double execution.</summary>
        public bool WasRolledBack { get; set; }
    }

    private sealed class PreparedWrite
    {
        public required string DestinationPath { get; init; }
        public required string TempPath { get; init; }
        public string? BackupPath { get; init; }
        public bool ExistedBeforeInstall => BackupPath is not null;
    }

    private sealed record InstallEntry(IArchiveEntry Entry, int OriginalIndex);

    private static int SelectBufferSize(long fileSize)
    {
        if (fileSize < 1_000_000)
            return 65_536;        // 64KB for small
        if (fileSize < 100_000_000)
            return 524_288;       // 512KB for medium
        return 2_097_152;         // 2MB for large
    }

    /// <summary>
    /// Extracts all files from <paramref name="mod"/> into <paramref name="targetRoot"/>.
    /// On partial failure, newly-created files are deleted and overwritten files are restored.
    /// </summary>
    /// <param name="persistentBackupFolder">
    /// When provided, overwritten-file backups are written here and NOT deleted after a successful
    /// install, enabling later user-initiated rollback via <c>TransactionService</c>.
    /// </param>
    public static async Task<InstallResult> InstallAsync(ModInfo mod, string targetRoot, InstallPlan? plan = null,
        string? persistentBackupFolder = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(mod.SourcePath))
            {
                var adapter = new DirectoryArchiveAdapter(mod.SourcePath);
                return await InstallAsync(adapter, targetRoot, plan, persistentBackupFolder, mod.ArchiveRootPrefix, cancellationToken);
            }
            else if (mod.SourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(mod.SourcePath);
                var adapter = new ZipArchiveAdapter(zip);
                return await InstallAsync(adapter, targetRoot, plan, persistentBackupFolder, mod.ArchiveRootPrefix, cancellationToken);
            }
            else
            {
                using var archive = ArchiveFactory.Open(mod.SourcePath);
                var adapter = new SharpCompressArchiveAdapter(archive);
                return await InstallAsync(adapter, targetRoot, plan, persistentBackupFolder, mod.ArchiveRootPrefix, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return new InstallResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Extracts all files from <paramref name="archive"/> into <paramref name="targetRoot"/>.
    /// Testable variant that accepts IArchive for unit testing.
    /// </summary>
    /// <param name="persistentBackupFolder">
    /// When provided, backups of overwritten files are written here and kept after a successful install.
    /// </param>
    public static async Task<InstallResult> InstallAsync(IArchive archive, string targetRoot, InstallPlan? plan = null,
        string? persistentBackupFolder = null, string archiveRootPrefix = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var writtenFiles = new List<string>();
        var rollbackFiles = new List<RollbackFile>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedRecords = new List<TransactionFileRecord>();
        var overwrittenRecords = new List<TransactionFileRecord>();

        bool isPersistentBackup = !string.IsNullOrEmpty(persistentBackupFolder);
        var backupRoot = isPersistentBackup
            ? persistentBackupFolder!
            : CreateBackupRoot(targetRoot);

        var planEntriesByPath = BuildPlanEntryMap(plan);
        var orderedEntries = OrderEntries(archive.Entries, planEntriesByPath);

        try
        {
            foreach (var installEntry in orderedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = installEntry.Entry;
                if (entry.IsDirectory)
                    continue;

                var rawKey = InstallerSafetyPolicy.NormalizeRelativePath(entry.Key);
                var relativePath = !string.IsNullOrEmpty(archiveRootPrefix) &&
                                   rawKey.StartsWith(archiveRootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? rawKey[archiveRootPrefix.Length..]
                    : rawKey;

                if (InstallerSafetyPolicy.IsJunkEntry(relativePath))
                {
                    AppLogger.Info($"[INSTALL_SKIP_JUNK] {relativePath}");
                    continue;
                }

                var destinationPath = PathSafety.GetSafePath(targetRoot, relativePath);

                planEntriesByPath.TryGetValue(relativePath, out var planEntry);

                var destinationExists = File.Exists(destinationPath);
                var action = ResolveAction(planEntry, relativePath, destinationExists);

                if (action == InstallConflictAction.CancelInstall)
                    throw new InvalidOperationException($"Install cancelled due to conflict policy: {relativePath}");

                if (action == InstallConflictAction.KeepExisting && destinationExists)
                {
                    AppLogger.Info($"[INSTALL_SKIP_KEEP_EXISTING] {relativePath}");
                    continue;
                }

                if (action == InstallConflictAction.Skip)
                {
                    AppLogger.Info($"[INSTALL_SKIP] {relativePath}");
                    continue;
                }

                if (action == InstallConflictAction.RenameIncoming && destinationExists)
                {
                    destinationPath = ResolveRenamePath(targetRoot, destinationPath, planEntry?.RenamedTargetPath);
                    destinationExists = File.Exists(destinationPath);
                }

                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                    TrackAndCreateDirectory(directory, createdDirectories);

                if (destinationExists && action == InstallConflictAction.BackupAndReplace)
                {
                    var preservePath = CreateTimestampedBackup(destinationPath);
                    ChangeHistoryService.Instance.Record(
                        ChangeHistoryAction.BackupCreated,
                        "Installer preserved overwritten file before replacement.",
                        affectedFile: destinationPath,
                        detail: preservePath);
                }

                var preparedWrite = PrepareWrite(destinationPath, backupRoot);
                var rollbackRecord = new RollbackFile
                {
                    DestinationPath = preparedWrite.DestinationPath,
                    BackupPath = preparedWrite.BackupPath,
                    WasCommitted = false,
                };
                rollbackFiles.Add(rollbackRecord);

                try
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        int bufferSize = SelectBufferSize(entry.Size);
                        AppLogger.Info($"[EXTRACT_START] {entry.Key} | size={entry.Size} bytes | buffer={bufferSize} bytes");
                        using (var destFile = new FileStream(preparedWrite.TempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                        {
                            await entryStream.CopyToAsync(destFile, bufferSize, cancellationToken);
                        }
                        AppLogger.Info($"[EXTRACT_OK] {entry.Key}");
                    }

                    CommitWrite(preparedWrite);
                    rollbackRecord.WasCommitted = true;
                    ChangeHistoryService.Instance.Record(
                        ChangeHistoryAction.Installed,
                        "Installer wrote file.",
                        affectedFile: preparedWrite.DestinationPath,
                        detail: action.ToString());

                    // Record per-file info for transaction tracking
                    if (preparedWrite.ExistedBeforeInstall)
                    {
                        overwrittenRecords.Add(new TransactionFileRecord
                        {
                            DestinationPath = destinationPath,
                            BackupPath = preparedWrite.BackupPath,
                        });
                    }
                    else
                    {
                        addedRecords.Add(new TransactionFileRecord
                        {
                            DestinationPath = destinationPath,
                            InstalledHash = TryComputeHash(destinationPath),
                        });
                    }
                }
                catch
                {
                    CleanupPreparedWrite(preparedWrite);
                    throw;
                }

                writtenFiles.Add(destinationPath);
            }

            // Only delete temp backup roots; persistent roots are kept for user-initiated rollback
            if (!isPersistentBackup)
                DeleteBackupRoot(backupRoot);

            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count,
                WrittenFiles = writtenFiles,
                AddedFileRecords = addedRecords,
                OverwrittenFileRecords = overwrittenRecords,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[EXTRACT_ERROR] rollback {rollbackFiles.Count} files", ex);
            var fileRollbackErrors = await RollbackAsync(rollbackFiles);
            var dirRollbackErrors  = RollbackDirectories(createdDirectories, targetRoot);
            DeleteBackupRoot(backupRoot); // always clean up on failure

            return new InstallResult
            {
                Success = false,
                IsPartial = rollbackFiles.Any(f => f.WasCommitted) || createdDirectories.Count > 0,
                FilesWritten = writtenFiles.Count,
                Error = ex.Message,
                RollbackErrors = [.. fileRollbackErrors, .. dirRollbackErrors],
                WrittenFiles = []
            };
        }
    }

    private static Dictionary<string, InstallPlanEntry> BuildPlanEntryMap(InstallPlan? plan)
    {
        return (plan?.Entries ?? [])
            .GroupBy(e => InstallerSafetyPolicy.NormalizeRelativePath(e.ArchivePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<InstallEntry> OrderEntries(
        IEnumerable<IArchiveEntry> archiveEntries,
        IReadOnlyDictionary<string, InstallPlanEntry> planEntriesByPath)
    {
        var materialized = archiveEntries
            .Where(e => !e.IsDirectory)
            .Select((entry, index) => new InstallEntry(entry, index))
            .ToList();

        var hasStopThePed = materialized.Any(e => InstallerSafetyPolicy.IsStopThePedFile(e.Entry.Key));
        var hasUltimateBackup = materialized.Any(e => InstallerSafetyPolicy.IsUltimateBackupFile(e.Entry.Key));

        return materialized
            .OrderBy(e =>
            {
                var key = InstallerSafetyPolicy.NormalizeRelativePath(e.Entry.Key);
                return planEntriesByPath.TryGetValue(key, out var planned)
                    ? planned.DependencyReason is null ? int.MaxValue : 0
                    : int.MaxValue;
            })
            .ThenBy(e => InstallerSafetyPolicy.GetInstallOrderPriority(e.Entry.Key, hasStopThePed, hasUltimateBackup))
            .ThenBy(e => e.OriginalIndex)
            .ToList();
    }

    private static InstallConflictAction ResolveAction(
        InstallPlanEntry? plannedEntry,
        string relativePath,
        bool destinationExists)
    {
        if (plannedEntry is not null)
            return plannedEntry.PlannedAction;

        return InstallerSafetyPolicy.DefaultConflictAction(relativePath, destinationExists);
    }

    private static string ResolveRenamePath(string targetRoot, string destinationPath, string? plannedRenamePath)
    {
        var candidate = plannedRenamePath;
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var relative = Path.GetRelativePath(targetRoot, candidate);
            return PathSafety.GetSafePath(targetRoot, relative);
        }

        var auto = InstallerSafetyPolicy.BuildIncomingRenamePath(destinationPath);
        var autoRelative = Path.GetRelativePath(targetRoot, auto);
        return PathSafety.GetSafePath(targetRoot, autoRelative);
    }

    private static string CreateTimestampedBackup(string destinationPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupBase = destinationPath + $".bak.{timestamp}";
        if (!File.Exists(backupBase))
        {
            File.Copy(destinationPath, backupBase, overwrite: false);
            return backupBase;
        }

        var index = 1;
        while (true)
        {
            var numbered = backupBase + $"_{index}";
            if (!File.Exists(numbered))
            {
                File.Copy(destinationPath, numbered, overwrite: false);
                return numbered;
            }
            index++;
        }
    }

    private static string? TryComputeHash(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 50 * 1024 * 1024) return null;
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
        }
        catch { return null; }
    }

    private static string CreateBackupRoot(string targetRoot)
    {
        var safeRoot = Directory.Exists(targetRoot)
            ? targetRoot
            : Path.GetTempPath();

        var backupRoot = Path.Combine(safeRoot, $".lspdfrmanager_rollback_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupRoot);
        return backupRoot;
    }

    private static void TrackAndCreateDirectory(string directory, HashSet<string> createdDirectories)
    {
        var current = Path.GetFullPath(directory);
        var missing = new Stack<string>();

        while (!Directory.Exists(current))
        {
            missing.Push(current);
            var parent = Directory.GetParent(current);
            if (parent is null)
                break;

            current = parent.FullName;
        }

        Directory.CreateDirectory(directory);

        foreach (var created in missing)
            createdDirectories.Add(created);
    }

    private static PreparedWrite PrepareWrite(string destinationPath, string backupRoot)
    {
        var destinationDir = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Invalid destination path: {destinationPath}");

        var tempPath = Path.Combine(destinationDir, $".lspdfrmanager_{Guid.NewGuid():N}.tmp");

        if (!File.Exists(destinationPath))
        {
            return new PreparedWrite
            {
                DestinationPath = destinationPath,
                TempPath = tempPath,
            };
        }

        var backupPath = Path.Combine(backupRoot, Guid.NewGuid().ToString("N") + ".bak");
        File.Copy(destinationPath, backupPath, overwrite: false);

        return new PreparedWrite
        {
            DestinationPath = destinationPath,
            TempPath = tempPath,
            BackupPath = backupPath
        };
    }

    private static void CommitWrite(PreparedWrite write)
    {
        File.Move(write.TempPath, write.DestinationPath, overwrite: true);
    }

    private static void CleanupPreparedWrite(PreparedWrite write)
    {
        try
        {
            if (File.Exists(write.TempPath))
                File.Delete(write.TempPath);

            if (write.ExistedBeforeInstall && File.Exists(write.BackupPath))
            {
                if (!File.Exists(write.DestinationPath))
                    File.Move(write.BackupPath!, write.DestinationPath);
                else
                    File.Delete(write.BackupPath!);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Prepared write cleanup failed for '{write.DestinationPath}': {ex.Message}");
        }
    }

    private static Task<List<string>> RollbackAsync(List<RollbackFile> files)
    {
        var errors = new List<string>();

        foreach (var file in files.AsEnumerable().Reverse())
        {
            if (!file.WasCommitted || file.WasRolledBack)
                continue;

            try
            {
                if (File.Exists(file.DestinationPath))
                    File.Delete(file.DestinationPath);

                if (file.ExistedBeforeInstall && File.Exists(file.BackupPath))
                {
                    var destinationDir = Path.GetDirectoryName(file.DestinationPath);
                    if (!string.IsNullOrEmpty(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Move(file.BackupPath!, file.DestinationPath);
                }

                file.WasRolledBack = true;
            }
            catch (Exception ex)
            {
                var msg = $"Rollback failed for '{file.DestinationPath}': {ex.Message}";
                errors.Add(msg);
                AppLogger.Warning(msg);
            }
        }

        return Task.FromResult(errors);
    }

    private static List<string> RollbackDirectories(HashSet<string> createdDirectories, string targetRoot)
    {
        var errors = new List<string>();
        var root = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var directory in createdDirectories.OrderByDescending(path => path.Length))
        {
            try
            {
                var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (fullDirectory.Equals(root, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Directory.Exists(fullDirectory) && !Directory.EnumerateFileSystemEntries(fullDirectory).Any())
                    Directory.Delete(fullDirectory);
            }
            catch (Exception ex)
            {
                var msg = $"Rollback directory failed for '{directory}': {ex.Message}";
                errors.Add(msg);
                AppLogger.Warning(msg);
            }
        }

        return errors;
    }

    private static void DeleteBackupRoot(string backupRoot)
    {
        try
        {
            if (Directory.Exists(backupRoot))
                Directory.Delete(backupRoot, recursive: true);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Rollback backup cleanup failed: {ex.Message}");
        }
    }
}

// ── Archive Adapters ──────────────────────────────────────────────────────

/// <summary>
/// Adapts a directory to IArchive interface.
/// </summary>
internal class DirectoryArchiveAdapter : IArchive
{
    private readonly string _source;

    public IEnumerable<IArchiveEntry> Entries
    {
        get
        {
            var files = Directory.GetFiles(_source, "*", SearchOption.AllDirectories);
            return files.Select(f => new DirectoryEntryAdapter(_source, f));
        }
    }

    public DirectoryArchiveAdapter(string source) => _source = source;
}

internal class DirectoryEntryAdapter : IArchiveEntry
{
    private readonly string _fullPath;

    public string Key { get; }
    public bool IsDirectory => false;
    public long Size => new FileInfo(_fullPath).Length;

    public DirectoryEntryAdapter(string source, string fullPath)
    {
        _fullPath = fullPath;
        Key = Path.GetRelativePath(source, fullPath);
    }

    public Stream OpenEntryStream() => File.OpenRead(_fullPath);
}

/// <summary>
/// Adapts System.IO.Compression.ZipFile to IArchive interface.
/// Streams entries directly from archive (no materialization).
/// </summary>
internal class ZipArchiveAdapter : IArchive
{
    private readonly ZipArchive _zipArchive;

    public IEnumerable<IArchiveEntry> Entries
    {
        get
        {
            return _zipArchive.Entries
                .Where(e => !e.FullName.EndsWith("/"))
                .Select(e => new ZipEntryAdapter(e));
        }
    }

    public ZipArchiveAdapter(ZipArchive zipArchive) => _zipArchive = zipArchive;
}

internal class ZipEntryAdapter : IArchiveEntry
{
    private readonly ZipArchiveEntry _entry;

    public string Key { get; }
    public bool IsDirectory => false;
    public long Size => _entry.Length;

    public ZipEntryAdapter(ZipArchiveEntry entry)
    {
        _entry = entry;
        Key = entry.FullName;
    }

    public Stream OpenEntryStream() => _entry.Open();
}

/// <summary>
/// Adapts SharpCompress.Archives.Archive to IArchive interface.
/// Streams entries directly from archive (no materialization).
/// WARNING: Streams from compressed archives may be non-seekable.
/// </summary>
internal class SharpCompressArchiveAdapter : IArchive
{
    private readonly SharpCompress.Archives.IArchive _archive;

    public IEnumerable<IArchiveEntry> Entries
    {
        get
        {
            return _archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e => new SharpCompressEntryAdapter(e));
        }
    }

    public SharpCompressArchiveAdapter(SharpCompress.Archives.IArchive archive) => _archive = archive;
}

internal class SharpCompressEntryAdapter : IArchiveEntry
{
    private readonly SharpCompress.Archives.IArchiveEntry _entry;

    public string Key { get; }
    public bool IsDirectory => false;
    public long Size => _entry.Size;

    public SharpCompressEntryAdapter(SharpCompress.Archives.IArchiveEntry entry)
    {
        _entry = entry;
        Key = entry.Key ?? "";
    }

    public Stream OpenEntryStream() => _entry.OpenEntryStream();
}
