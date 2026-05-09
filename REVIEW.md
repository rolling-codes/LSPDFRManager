# LSPDFRManager-3.5.2 — Code Review Report

**Reviewed:** 2026-05-09  
**Depth:** Standard (with deep cross-file analysis on focus areas)  
**Status:** issues_found

---

## Summary

Reviewed all eight priority files plus supporting services. The core install path
(FileInstaller → InstallQueue → ModLibraryService) is largely sound, but several
issues range from BLOCKER-severity safety violations to meaningful null-safety
gaps. The most serious findings are:

1. **OpenIvExecutor** violates the documented rollback contract by pushing the
   destination path before the write succeeds, leaving a dangling rollback entry
   on write failure.
2. **BackupService.RestoreFromBackupAsync** extracts ZIP entries with no path
   traversal check, allowing a crafted backup file to write arbitrary files
   anywhere on the filesystem.
3. **SmartInstallPlanner** leaks a SharpCompress `IArchive` into application
   code without PathSafety validation, contradicting the adapter boundary rule
   and allowing path traversal in plan entries that are later displayed (but not
   directly written) to the user.
4. **InstallQueue.Dispose** disposes `_signal` before cancelling the worker loop,
   creating a race where the worker can throw `ObjectDisposedException` instead
   of observing the cancellation.
5. **RestorePointService.Load** has a bare `catch {}` block that silently
   discards all errors.

---

## BLOCKER Issues

### BL-01: OpenIvExecutor — file pushed to rollback list before write succeeds

**File:** `Core/CarInstall/OpenIvExecutor.cs:63-68`

The destination path is pushed onto `writtenFiles` *before* `SafeCopyAsync` is
called. If `SafeCopyAsync` throws, the path is in the rollback stack even though
no file was written. `RollbackAsync` will then attempt to delete a file that does
not exist (harmless in the best case), but the write attempt will also have
created the destination file via `File.Create` before the write fails, so that
partially-written file is in the rollback list and *will* be deleted correctly —
however the `writtenFiles.Count` reported in the `InstallResult` will be one too
high (the entry that failed is counted). More critically, this violates the
project's documented invariant:

> Add file to rollback list ONLY after successful copy

**Current code:**
```csharp
writtenFiles.Push(destPath);               // ← pushed before write

using (var entryStream = sourceEntry.OpenEntryStream())
{
    await SafeCopyAsync(entryStream, destPath, sourceEntry.Size, ct);
}
```

**Fix:** push after `SafeCopyAsync` returns:
```csharp
using (var entryStream = sourceEntry.OpenEntryStream())
{
    await SafeCopyAsync(entryStream, destPath, sourceEntry.Size, ct);
}

writtenFiles.Push(destPath);   // only here, after successful write
```

---

### BL-02: BackupService.RestoreFromBackupAsync — no path traversal protection

**File:** `Services/BackupService.cs:52-57`

`entry.FullName` is combined with `AppDataPaths.Root` with no validation that
the resulting path stays inside `AppDataPaths.Root`. A crafted ZIP with entries
like `../../Windows/System32/evil.dll` would write to arbitrary filesystem
locations. This is a classic zip-slip vulnerability.

**Current code:**
```csharp
var destination = Path.Combine(AppDataPaths.Root, entry.FullName);
var directory = Path.GetDirectoryName(destination);
...
entry.ExtractToFile(destination, overwrite: true);
```

**Fix:** Validate with `PathSafety.GetSafePath` (which already exists in the
codebase and is designed for exactly this):
```csharp
string destination;
try
{
    destination = PathSafety.GetSafePath(AppDataPaths.Root, entry.FullName);
}
catch (InvalidOperationException)
{
    AppLogger.Warning($"Skipping unsafe backup entry: {entry.FullName}");
    continue;
}
var directory = Path.GetDirectoryName(destination);
...
entry.ExtractToFile(destination, overwrite: true);
```

---

### BL-03: InstallQueue.Dispose — SemaphoreSlim disposed before worker exits

**File:** `Core/InstallQueue.cs:176-181`

`Dispose` calls `_cts.Cancel()` and then immediately `_signal.Dispose()`. The
background worker task is still running at the point `_signal.Dispose()` is
called. If the worker is blocked in `_signal.WaitAsync(_cts.Token)`, disposing
the semaphore while it is being waited causes `ObjectDisposedException`, not a
clean `OperationCanceledException`. The worker task's exception then goes
unobserved (the `_worker` Task is never awaited).

**Current code:**
```csharp
public void Dispose()
{
    _cts.Cancel();
    _signal.Dispose();   // ← races with worker
    _cts.Dispose();
}
```

**Fix:** Wait for the worker to exit before disposing:
```csharp
public void Dispose()
{
    _cts.Cancel();
    try { _worker.Wait(TimeSpan.FromSeconds(5)); } catch { }
    _signal.Dispose();
    _cts.Dispose();
}
```

---

### BL-04: SmartInstallPlanner — SharpCompress type leaks outside adapter layer; no PathSafety on plan entries

**File:** `Services/SmartInstallPlanner.cs:19-45`

Two violations of project rules in the same block:

1. `ArchiveFactory.Open` returns a raw `SharpCompress.Archives.IArchive`. The
   cast `as SharpCompress.Archives.IArchive` is redundant but more importantly
   SharpCompress types are used directly in the planner, violating the rule that
   "No SharpCompress types outside adapter layer."

2. The `targetPath` for each plan entry is built with:
   ```csharp
   var targetPath = Path.Combine(gtaPath, entryPath);
   ```
   There is no `PathSafety.GetSafePath` call. The plan checks `entryPath.Contains("..")` as a heuristic (line 32) but that check misses encoded separators and absolute paths. The plan is used to *display* what will be overwritten — if an attacker-controlled archive is processed, the displayed `TargetPath` paths will be wrong and could mislead the user about what is about to be overwritten.

   The `..` check only flags the risk, it does not skip the entry. The entry with `InstallRisk.Incompatible` is still added to `entries` and shown to the user.

**Fix for (1):** Use the existing `SharpCompressArchiveAdapter` that already wraps the SharpCompress archive behind `IArchive`.

**Fix for (2):** Use `PathSafety.GetSafePath` (wrap in try/catch and mark the entry as `InstallRisk.Incompatible` + skip on exception) instead of the string `".."` heuristic.

---

### BL-05: ModLibraryService.SetEnabled — TOCTOU race between read and lock

**File:** `Services/ModLibraryService.cs:51-68`

`target` is resolved from `Mods` outside the `_mutationLock`, then used inside
it. Between the `UiDispatcher.Invoke` that finds `target` and the
`lock(_mutationLock)` block that calls `_fileService.SetEnabled(target, ...)`,
another thread could call `Uninstall(id)` which removes the mod from `Mods` and
deletes its files. The lock then proceeds to rename files that no longer exist.
While the CLAUDE.md states "assume single-threaded UI access," `SetEnabled` is
called from the background `InstallQueue` worker as well (indirectly via events),
making this a real race condition.

**File:** `Services/ModLibraryService.cs:108-127` (same pattern in `Reorder` — but `Reorder` accesses `Mods` directly inside the lock, so it is safe; only `SetEnabled` and `Uninstall` have this pattern.)

**Fix:** Resolve `target` inside the `lock`:
```csharp
public void SetEnabled(Guid id, bool enabled)
{
    lock (_mutationLock)
    {
        InstalledMod? target = null;
        UiDispatcher.Invoke(() => target = Mods.FirstOrDefault(mod => mod.Id == id));

        if (target is null || target.IsEnabled == enabled)
            return;

        _fileService.SetEnabled(target, enabled);
        ModUpdated?.Invoke(target);
        Save();
    }
}
```

---

## WARNING Issues

### WR-01: RestorePointService.Load — bare catch swallows all errors silently

**File:** `Services/RestorePointService.cs:22-23`

```csharp
catch { _points = []; }
```

No logging, no user notification. A corrupted index file (disk full, incomplete
write) will be silently reset to empty, wiping all restore point metadata without
any indication to the user. The CLAUDE.md explicitly lists "Catching and
swallowing install exceptions" as an anti-pattern.

**Fix:**
```csharp
catch (Exception ex)
{
    AppLogger.Warning($"Failed to load restore points index: {ex.Message}");
    _points = [];
}
```

---

### WR-02: SmartInstallPlanner — stream from archive entry is never disposed

**File:** `Services/SmartInstallPlanner.cs:36`

```csharp
try { readmeContent = entry.OpenEntryStream().ReadToEnd(); } catch { }
```

`OpenEntryStream()` returns a `Stream` that is never disposed. For SharpCompress
streams this can prevent the archive from being fully released and may prevent
subsequent entries from being read correctly (SharpCompress streams are
forward-only and position-sensitive).

**Fix:**
```csharp
try
{
    using var s = entry.OpenEntryStream();
    readmeContent = s.ReadToEnd();
}
catch { }
```

---

### WR-03: SmartInstallPlanner — bare catch swallows readme read errors

**File:** `Services/SmartInstallPlanner.cs:36`

The inner `catch { }` on the readme read silently discards exceptions with no
logging. While failing to read a readme is non-critical, the pattern is an
anti-pattern per CLAUDE.md and makes debugging impossible.

**Fix:** At minimum log at debug/info level:
```csharp
catch (Exception ex)
{
    AppLogger.Info($"Could not read readme from archive: {ex.Message}");
}
```

---

### WR-04: JsonFileStore — FileLock is a static field, shared across all instances

**File:** `Services/JsonFileStore.cs:5`

```csharp
private static readonly object FileLock = new();
```

This is a `static` lock on a generic class. All `JsonFileStore<T>` instances
(library.json, config.json, configs.json) share the same lock object. This means
saving `library.json` blocks concurrent saves to `config.json`. This is
overly broad serialization. While correctness is preserved (no corruption), under
load (e.g., auto-backup fires while an install completes) all file saves are
serialized behind a single lock. Given the project's singleton design and "assume
single-threaded UI access," this is unlikely to cause a deadlock but it is a
hidden global bottleneck. The lock should be per-instance.

**Fix:** Change to instance field:
```csharp
private readonly object _fileLock = new();
// then reference _fileLock instead of FileLock
```

---

### WR-05: BackupService — Task.Run wraps sync ZIP operations without CancellationToken

**File:** `Services/BackupService.cs:19-31` and `47-60`

Both `CreateBackupAsync` and `RestoreFromBackupAsync` use `Task.Run(() => { ... })`
without passing a `CancellationToken` to the lambda or to `Task.Run`. If the
caller cancels, the backup/restore operation continues running in the thread pool
until completion. `BackupService` accepts no `CancellationToken` from callers at
the top level at all. This makes long backup operations (large libraries)
unresponsive to cancellation.

**Fix:** Accept and pass a `CancellationToken` through:
```csharp
public async Task<string> CreateBackupAsync(
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default)
{
    ...
    await Task.Run(() =>
    {
        using var zip = ZipFile.Open(backupPath, ZipArchiveMode.Create);
        foreach (var filePath in GetFilesToBackup())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ...
        }
    }, cancellationToken);
```

---

### WR-06: ModLibraryService.Uninstall — nested lock acquisition (double-lock risk)

**File:** `Services/ModLibraryService.cs:147-163`

`Uninstall` acquires `_mutationLock` and then calls `Remove(id)`, which also
acquires `_mutationLock`. In C#, `lock` is reentrant on the same thread, so this
does not deadlock. However it is fragile — if `Remove` is ever called from a
different thread the inner lock will block. The intent is unclear: is `Remove`
supposed to be called inside an existing lock scope? Document or refactor to call
an internal `RemoveInternal` that does not acquire the lock.

**Fix:** Extract a lock-free `RemoveCore` and call it from both `Remove` and
`Uninstall`:
```csharp
private void RemoveCore(Guid id)   // called only when lock is already held
{
    UiDispatcher.Invoke(() =>
    {
        var mod = Mods.FirstOrDefault(item => item.Id == id);
        if (mod is not null)
            Mods.Remove(mod);
    });
    Save();
}
```

---

### WR-07: OpenIvExecutor.RollbackAsync — respects CancellationToken during rollback

**File:** `Core/CarInstall/OpenIvExecutor.cs:186`

```csharp
ct.ThrowIfCancellationRequested();
```

Inside rollback, `ct.ThrowIfCancellationRequested()` means a cancellation request
that originally triggered the rollback will also abort the rollback mid-way,
leaving files on disk that should have been deleted. During rollback the
CancellationToken should not be observed (or a separate, non-cancellable path
should be used).

**Fix:** Remove `ct.ThrowIfCancellationRequested()` from inside `RollbackAsync`,
or create a new unconditional rollback path:
```csharp
private static Task RollbackAsync(Stack<string> files, CancellationToken ct)
{
    // Do NOT honour ct here — rollback must complete regardless of cancellation
    int rollbackCount = files.Count;
    ...
    while (files.Count > 0)
    {
        var file = files.Pop();
        try
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception ex) { ... }
    }
    return Task.CompletedTask;
}
```

---

### WR-08: InstalledMod.InstalledAt — uses local time, not UTC

**File:** `Domain/InstalledMod.cs:50`

```csharp
public DateTime InstalledAt { get; set; } = DateTime.Now;
```

`DateTime.Now` returns local time without time-zone information. If library.json
is read on a machine in a different time zone (or after a daylight saving change),
the ordering in `ModLibraryService.Load` (`OrderByDescending(mod => mod.InstalledAt)`)
will produce unexpected results. The field is labelled "UTC timestamp" in its
XML doc but is populated with local time.

**Fix:**
```csharp
public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
```

---

### WR-09: BatchReinstallService.LoadManifestAsync — ZipFile.ExtractToDirectory has no path traversal protection

**File:** `Services/BatchReinstallService.cs:79`

```csharp
ZipFile.ExtractToDirectory(manifestPath, tempDirectory);
```

`ZipFile.ExtractToDirectory` in .NET 8 does throw on path traversal in most
cases, but the extraction is done into a temp directory from a user-supplied
`manifestPath`. If `manifestPath` is attacker-controlled (e.g., a community mod
"reinstall bundle"), this is a zip-slip vector. Unlike BackupService, the
consequences are limited to `tempDirectory`, but the extracted `manifest.json`
file is then deserialized with `JsonSerializer.Deserialize<ModManifest>` and the
`mod.SourceArchivePath` values from it are used to build install paths (line
90-92), meaning the manifest can redirect installs to arbitrary paths.

**Fix:** Validate that `mod.SourceArchivePath` (after manifest deserialization)
stays within `tempDirectory` before rewriting it:
```csharp
var extractedArchive = Path.Combine(tempDirectory, Path.GetFileName(mod.SourceArchivePath));
// Ensure the resolved path is inside tempDirectory
var fullExtracted = Path.GetFullPath(extractedArchive);
var fullTemp = Path.GetFullPath(tempDirectory).TrimEnd(Path.DirectorySeparatorChar)
               + Path.DirectorySeparatorChar;
if (fullExtracted.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase)
    && File.Exists(fullExtracted))
{
    mod.SourceArchivePath = fullExtracted;
}
```

---

### WR-10: ModDetector — SharpCompress used directly (adapter boundary violation)

**File:** `Services/ModDetector.cs:4,162`

```csharp
using SharpCompress.Archives;
...
using var archive = ArchiveFactory.Open(path);
```

SharpCompress types are used directly in `ModDetector` (outside the adapter
layer). The CLAUDE.md rule is: "No SharpCompress types outside adapter layer."
While `ModDetector` only uses the archive for listing (not writing), this couples
the detector to SharpCompress internals and breaks the isolation contract.

**Fix:** Extract the listing into a method that uses the same
`SharpCompressArchiveAdapter`/`IArchive` abstraction.

---

## INFO Issues

### IN-01: InstallQueue.Dispose — _worker Task is never observed after Dispose

**File:** `Core/InstallQueue.cs:176-181`

After cancellation is requested, the `_worker` Task may complete with an
`AggregateException` wrapping `ObjectDisposedException` (see BL-03). This
unobserved task exception will be silently ignored after .NET 4.5+ GC finalizes
the Task, but it means any final error from the worker is permanently lost.

**Fix:** As part of the BL-03 fix (awaiting `_worker.Wait`), the exception is
naturally observed.

---

### IN-02: FileInstaller — SharpCompress adapter file leaks on IArchive boundary

**File:** `Services/FileInstaller.cs:60-63`

When the source is a non-ZIP compressed archive, `ArchiveFactory.Open` returns
an `IArchive` that is wrapped in `SharpCompressArchiveAdapter` but the underlying
`archive` variable is disposed by the `using` block. The adapter holds a reference
to the disposed archive. Because `InstallAsync(IArchive)` enumerates entries
lazily, if `Entries` is re-evaluated after `archive` is disposed, it would throw.
In practice the current code only enumerates once, but this is fragile. The
adapter's lifetime is tied to a `using` block above the call to `InstallAsync`,
which correctly handles this, but it should be documented.

---

### IN-03: DlcListService.NormalizePackName — returns only the last path segment

**File:** `Services/DlcListService.cs:122`

```csharp
return parts.Length == 0 ? string.Empty : parts[^1];
```

If a DLC path like `dlcpacks:/some/nested/pack/` is in `dlclist.xml`, only
`"pack"` is returned. This means `EntryMatchesPack` could false-positive match
unrelated packs that share the same last segment. While GTA DLC paths are
conventionally single-segment after `dlcpacks:/`, this is an assumption that
could cause incorrect removal of unrelated DLC entries.

---

### IN-04: LspdfrApiClient — HttpClient instantiated per instance, not shared

**File:** `Services/LspdfrApiClient.cs:31`

```csharp
private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
```

`LspdfrApiClient` is a singleton so there is only one `HttpClient` instance,
which is correct. However the `HttpClient` is never disposed (no `IDisposable`
implementation). For a singleton this is acceptable since it lives for the app
lifetime, but the lack of `IDisposable` means it cannot be cleaned up if the
singleton pattern ever changes.

---

_Reviewed: 2026-05-09_  
_Reviewer: Claude (adversarial code review)_  
_Depth: deep_
