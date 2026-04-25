using LSPDFRManager.Models;
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
                    using (var destFile = File.Create(dest))
                    {
                        await entryStream.CopyToAsync(destFile);
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
/// Materializes all entries to memory (so archive can be disposed).
/// </summary>
internal class ZipArchiveAdapter : IArchive
{
    private IEnumerable<IArchiveEntry>? _materializedEntries;
    private readonly ZipArchive _zipArchive;

    public IEnumerable<IArchiveEntry> Entries
    {
        get
        {
            _materializedEntries ??= _zipArchive.Entries
                .Where(e => !e.FullName.EndsWith("/"))
                .Select(e =>
                {
                    using var stream = e.Open();
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return new ZipEntryAdapter(e.FullName, ms.ToArray());
                })
                .ToList();

            return _materializedEntries;
        }
    }

    public ZipArchiveAdapter(ZipArchive zipArchive) => _zipArchive = zipArchive;
}

internal class ZipEntryAdapter : IArchiveEntry
{
    private readonly byte[] _content;

    public string Key { get; }
    public bool IsDirectory => false;
    public long Size => _content.Length;

    public ZipEntryAdapter(string fullName, byte[] content)
    {
        Key = fullName;
        _content = content;
    }

    public Stream OpenEntryStream() => new MemoryStream(_content);
}

/// <summary>
/// Adapts SharpCompress.Archives.Archive to IArchive interface.
/// Materializes all entries to memory (so archive can be disposed).
/// </summary>
internal class SharpCompressArchiveAdapter : IArchive
{
    private IEnumerable<IArchiveEntry>? _materializedEntries;
    private readonly SharpCompress.Archives.IArchive _archive;

    public IEnumerable<IArchiveEntry> Entries
    {
        get
        {
            _materializedEntries ??= _archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e =>
                {
                    using var stream = e.OpenEntryStream();
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return new SharpCompressEntryAdapter(e.Key ?? "", ms.ToArray());
                })
                .ToList();

            return _materializedEntries;
        }
    }

    public SharpCompressArchiveAdapter(SharpCompress.Archives.IArchive archive) => _archive = archive;
}

internal class SharpCompressEntryAdapter : IArchiveEntry
{
    private readonly byte[] _content;

    public string Key { get; }
    public bool IsDirectory => false;
    public long Size => _content.Length;

    public SharpCompressEntryAdapter(string key, byte[] content)
    {
        Key = key;
        _content = content;
    }

    public Stream OpenEntryStream() => new MemoryStream(_content);
}