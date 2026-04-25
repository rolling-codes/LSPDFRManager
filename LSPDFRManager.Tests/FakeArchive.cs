using LSPDFRManager.Services;

namespace LSPDFRManager.Tests;

/// <summary>
/// Fake archive entry backed by memory or a custom stream factory.
/// </summary>
public class FakeArchiveEntry : IArchiveEntry
{
    private readonly Func<Stream> _streamFactory;

    public string Key { get; }
    public bool IsDirectory { get; }
    public long Size { get; }

    public FakeArchiveEntry(string key, byte[] content)
    {
        Key = key;
        IsDirectory = key.EndsWith('/');
        Size = content.Length;
        _streamFactory = () => new MemoryStream(content);
    }

    public FakeArchiveEntry(string key, Func<Stream> streamFactory, long size = 0)
    {
        Key = key;
        IsDirectory = false;
        Size = size;
        _streamFactory = streamFactory;
    }

    public Stream OpenEntryStream() => _streamFactory();
}

/// <summary>
/// Fake archive implementation backed by a list of entries.
/// </summary>
public class FakeArchive : IArchive
{
    public IEnumerable<IArchiveEntry> Entries { get; }

    public FakeArchive(IEnumerable<IArchiveEntry> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Stream that throws exception after N bytes read (simulates corruption mid-extract).
/// </summary>
public class ThrowingStream : Stream
{
    private int _bytesRead;
    private readonly int _failAfter;
    private readonly MemoryStream _backingStream;

    public ThrowingStream(byte[] content, int failAfter)
    {
        _backingStream = new MemoryStream(content);
        _failAfter = failAfter;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _backingStream.Length;
    public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesRead >= _failAfter)
            throw new IOException("Stream corruption simulated");

        int toRead = Math.Min(count, _failAfter - _bytesRead);
        int read = _backingStream.Read(buffer, offset, toRead);
        _bytesRead += read;
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _backingStream?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Factory for creating test archive scenarios.
/// </summary>
public static class FakeArchiveFactory
{
    public static IArchive CreatePathTraversalArchive()
    {
        return new FakeArchive(new[]
        {
            new FakeArchiveEntry("legitimate.dll", new byte[] {1, 2, 3}),
            new FakeArchiveEntry("../../escape.exe", new byte[] {9, 8, 7})
        });
    }

    public static IArchive CreateMidStreamFailureArchive(int failAfterBytes = 5)
    {
        var entries = new List<IArchiveEntry>
        {
            new FakeArchiveEntry("file1.dll", new byte[] {1, 2, 3}),
            new FakeArchiveEntry("file2.dll", new byte[] {4, 5, 6}),
            // Third file throws mid-read
            new FakeArchiveEntry("file3.dll",
                () => new ThrowingStream(new byte[] {7, 8, 9, 10, 11, 12}, failAfterBytes),
                size: 6)
        };

        return new FakeArchive(entries);
    }

    public static IArchive CreateDeepNestedPathArchive()
    {
        return new FakeArchive(new[]
        {
            new FakeArchiveEntry("a/b/c/d/e/f/g/deep.dll", new byte[] {1, 2, 3})
        });
    }

    public static IArchive CreateLargeFileArchive()
    {
        var largeContent = new byte[10_000_000]; // 10 MB
        Array.Fill(largeContent, (byte)42);

        return new FakeArchive(new[]
        {
            new FakeArchiveEntry("large.dat", largeContent)
        });
    }

    public static IArchive CreateManyFilesArchive(int fileCount = 1000)
    {
        var entries = Enumerable.Range(0, fileCount)
            .Select(i => new FakeArchiveEntry($"file{i:D5}.dll", new byte[] {(byte)(i % 256)}))
            .Cast<IArchiveEntry>()
            .ToList();

        return new FakeArchive(entries);
    }

    public static IArchive CreateStreamOpenFailureArchive()
    {
        var entries = new List<IArchiveEntry>
        {
            new FakeArchiveEntry("safe.dll", new byte[] {1, 2, 3}),
            new FakeArchiveEntry("fail.dll", () => throw new IOException("Cannot open stream"))
        };

        return new FakeArchive(entries);
    }

    public static IArchive CreateCleanArchive(params string[] filePaths)
    {
        var entries = filePaths
            .Select(path => new FakeArchiveEntry(path, new byte[] {1, 2, 3}))
            .Cast<IArchiveEntry>()
            .ToList();

        return new FakeArchive(entries);
    }

    public static IArchive CreateEmptyArchive()
    {
        return new FakeArchive(Enumerable.Empty<IArchiveEntry>());
    }
}
