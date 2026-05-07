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
    }

    private sealed class PreparedWrite
    {
        public required string DestinationPath { get; init; }
        public required string TempPath { get; init; }
        public string? BackupPath { get; init; }
        public bool ExistedBeforeInstall => BackupPath is not null;
    }

    private static int SelectBufferSize(long fileSize)
    {
        if (fileSize < 1_000_000)
            return 65_536;        // 64KB for small
        if (fileSize < 100_000_000)
            return 524_288;       // 512KB for medium
        return 2_097_152;         // 2MB for large
    }

    /// <summary>
    /// Extracts all files from <paramref name="mod"/> into <paramref name="targetRoot"/>,
    /// overwriting any existing files. Returns a result indicating success/failure.
    /// On partial failure, newly-created files are deleted and overwritten files are restored.
    /// </summary>
    public static async Task<InstallResult> InstallAsync(ModInfo mod, string targetRoot)
    {
        try
        {
            if (Directory.Exists(mod.SourcePath))
            {
                var adapter = new DirectoryArchiveAdapter(mod.SourcePath);
                return await InstallAsync(adapter, targetRoot);
            }
            else if (mod.SourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(mod.SourcePath);
                var adapter = new ZipArchiveAdapter(zip);
                return await InstallAsync(adapter, targetRoot);
            }
            else
            {
                using var archive = ArchiveFactory.Open(mod.SourcePath);
                var adapter = new SharpCompressArchiveAdapter(archive);
                return await InstallAsync(adapter, targetRoot);
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
    public static async Task<InstallResult> InstallAsync(IArchive archive, string targetRoot)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var writtenFiles = new List<string>();
        var rollbackFiles = new List<RollbackFile>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var backupRoot = CreateBackupRoot(targetRoot);

        try
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;

                var dest = PathSafety.GetSafePath(targetRoot, entry.Key);
                var dir = Path.GetDirectoryName(dest);

                if (!string.IsNullOrEmpty(dir))
                    TrackAndCreateDirectory(dir, createdDirectories);

                var preparedWrite = PrepareWrite(dest, backupRoot);

                try
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        int bufferSize = SelectBufferSize(entry.Size);
                        AppLogger.Info($"[EXTRACT_START] {entry.Key} | size={entry.Size} bytes | buffer={bufferSize} bytes");
                        using (var destFile = new FileStream(preparedWrite.TempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                        {
                            await entryStream.CopyToAsync(destFile, bufferSize);
                        }
                        AppLogger.Info($"[EXTRACT_OK] {entry.Key}");
                    }

                    CommitWrite(preparedWrite);
                }
                catch
                {
                    CleanupPreparedWrite(preparedWrite);
                    throw;
                }

                rollbackFiles.Add(new RollbackFile
                {
                    DestinationPath = preparedWrite.DestinationPath,
                    BackupPath = preparedWrite.BackupPath,
                });
                writtenFiles.Add(dest);
            }

            DeleteBackupRoot(backupRoot);

            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count,
                WrittenFiles = writtenFiles
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[EXTRACT_ERROR] rollback {rollbackFiles.Count} files", ex);
            await RollbackAsync(rollbackFiles);
            RollbackDirectories(createdDirectories, targetRoot);
            DeleteBackupRoot(backupRoot);

            return new InstallResult
            {
                Success = false,
                IsPartial = rollbackFiles.Count > 0 || createdDirectories.Count > 0,
                FilesWritten = writtenFiles.Count,
                Error = ex.Message,
                WrittenFiles = []
            };
        }
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

    private static Task RollbackAsync(List<RollbackFile> files)
    {
        foreach (var file in files.AsEnumerable().Reverse())
        {
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
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Rollback '{file.DestinationPath}' failed: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private static void RollbackDirectories(HashSet<string> createdDirectories, string targetRoot)
    {
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
                AppLogger.Warning($"Rollback directory '{directory}' failed: {ex.Message}");
            }
        }
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
