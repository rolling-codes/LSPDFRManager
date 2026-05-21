namespace LSPDFRManager.Services;

/// <summary>
/// Represents a single entry (file or directory) in an archive.
/// </summary>
public interface IArchiveEntry
{
    string Key { get; }
    bool IsDirectory { get; }
    long Size { get; }
    Stream OpenEntryStream();
}

/// <summary>
/// Represents an archive (ZIP, RAR, 7z, or fake).
/// Abstracts away SharpCompress/System.IO.Compression for testability.
/// </summary>
public interface IArchive
{
    IEnumerable<IArchiveEntry> Entries { get; }
}
