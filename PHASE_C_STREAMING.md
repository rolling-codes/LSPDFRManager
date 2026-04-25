# Phase C: Streaming Archive Extraction

## Status
**Planned, not started.** Current implementation (commit eb158c8) materializes archives fully in memory. This is safe but inefficient for large mods (50MB+).

## Scope
Replace eager materialization with streaming extraction. No safety/rollback changes.

## What Changes
```csharp
// Before (current):
_materializedEntries ??= archive.Entries
    .Select(e => { ... materialize to memory ... })
    .ToList();

// After (Phase C):
public async IAsyncEnumerable<IArchiveEntry> GetEntriesAsync(CancellationToken ct)
{
    foreach (var e in _archive.Entries)
    {
        ct.ThrowIfCancellationRequested();
        yield return new SharpEntryAdapter(e);
        await Task.Yield(); // prevents UI freeze on huge archives
    }
}
```

## What Stays Locked
- `PathSafety.GetSafePath()` — no changes
- `writtenFiles` list + rollback order — must stay same
- `InstallResult` contract — maintain exactly
- Error propagation chain — unchanged

## Core Loop Target
```csharp
await foreach (var entry in archive.GetEntriesAsync(ct))
{
    if (entry.IsDirectory) continue;

    var dest = PathSafety.GetSafePath(root, entry.Key);
    var dir = Path.GetDirectoryName(dest);

    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

    using var inStream = await entry.OpenStreamAsync(ct);
    using var outStream = File.Create(dest);

    await inStream.CopyToAsync(outStream, ct);

    writtenFiles.Add(dest); // still after copy (not before!)
}
```

## Interface Evolution (Additive Only)
```csharp
public interface IArchive
{
    IEnumerable<IArchiveEntry> Entries { get; } // keep for tests
    IAsyncEnumerable<IArchiveEntry> GetEntriesAsync(CancellationToken ct = default); // new
}

public interface IArchiveEntry
{
    string Key { get; }
    bool IsDirectory { get; }
    long Size { get; }

    Stream OpenEntryStream(); // existing
    Task<Stream> OpenStreamAsync(CancellationToken ct = default); // new
}
```

## Implementations to Update
1. **SharpCompressArchiveAdapter** — main win (no materialization)
2. **ZipArchiveAdapter** — optional (ZipArchive already streams)
3. **DirectoryArchiveAdapter** — add async variant
4. **FakeArchive** (tests) — wrap sync with Task.Yield(), don't rewrite

## Validation Checklist
- [ ] All 98 existing tests still pass
- [ ] Memory profile flat on 100MB+ archives
- [ ] Rollback works on mid-stream failure
- [ ] Rollback works on corrupt stream
- [ ] UI responsive during large archive installs
- [ ] CancellationToken properly threaded

## Hidden Traps
1. Don't mix sync/async wrong (e.g., `Task.Run(() => stream.CopyTo(...))`)
2. Thread CancellationToken through entire call chain
3. Keep `writtenFiles.Add(dest)` after copy, not before
4. Include `await Task.Yield()` in enumerators (prevents UI starvation)
5. Don't break fake archives—just wrap them

## Next Steps (When Starting Phase C)
1. Add async methods to IArchive / IArchiveEntry interfaces
2. Implement GetEntriesAsync() in all adapters
3. Update FileInstaller.InstallAsync() core loop (await foreach)
4. Run full test suite (should all pass)
5. Profile memory on 100MB+ archive (should be flat)
6. Test rollback on mid-stream failure

## Expected Outcome
- Memory usage: constant (not proportional to archive size)
- UI responsiveness: maintained (Task.Yield() in enumerator)
- Correctness: unchanged (same safety guarantees)
- Test coverage: 98+ still passing
