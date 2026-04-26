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
    /// On partial failure, rolls back all written files.
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
        var writtenFiles = new List<string>();

        try
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;

                var dest = PathSafety.GetSafePath(targetRoot, entry.Key);
                var dir = Path.GetDirectoryName(dest);

                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                // Track file before write; if copy fails, rollback will clean it up
                writtenFiles.Add(dest);

                using (var entryStream = entry.OpenEntryStream())
                {
                    int bufferSize = SelectBufferSize(entry.Size);
                    using (var destFile = File.Create(dest))
                    {
                        await entryStream.CopyToAsync(destFile, bufferSize);
                    }
                }
            }

            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count
            };
        }
        catch (Exception ex)
        {
            await RollbackAsync(writtenFiles);

            return new InstallResult
            {
                Success = false,
                IsPartial = writtenFiles.Count > 0,
                FilesWritten = writtenFiles.Count,
                Error = ex.Message
            };
        }
    }

    private static Task RollbackAsync(List<string> files)
    {
        foreach (var file in files.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Swallow cleanup errors; already in error state
            }
        }

        return Task.CompletedTask;
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