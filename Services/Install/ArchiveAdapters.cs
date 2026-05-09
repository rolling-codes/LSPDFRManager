using SharpCompress.Archives;

namespace LSPDFRManager.Services;

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

    public IEnumerable<IArchiveEntry> Entries =>
        _zipArchive.Entries
            .Where(e => !e.FullName.EndsWith("/"))
            .Select(e => new ZipEntryAdapter(e));

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

    public IEnumerable<IArchiveEntry> Entries =>
        _archive.Entries
            .Where(e => !e.IsDirectory)
            .Select(e => new SharpCompressEntryAdapter(e));

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
