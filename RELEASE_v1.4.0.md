# Release v1.4.0 — Streaming Installer (Phase C)

## Summary

Phase C streaming optimization complete. True streaming pipeline with adaptive buffering replaces archive materialization. Memory usage reduced from archive-size to constant buffer-size.

## Performance Improvements

### Memory Footprint
- **Before**: 200MB ZIP → 200MB+ memory peak during install
- **After**: 200MB ZIP → ~2MB memory peak (buffer size)
- **Impact**: Large mods now installable on memory-constrained systems

### Buffer Strategy (Adaptive)
- Small files (< 1MB): 64KB buffer
- Medium files (1-100MB): 512KB buffer  
- Large files (> 100MB): 2MB buffer

## Architecture Changes

### Archive Adapters (Refactored)
- **ZipArchiveAdapter**: Streams directly from ZipArchiveEntry.Open()
- **SharpCompressArchiveAdapter**: Streams directly from archive entries
- **Removal**: No more MemoryStream materialization, no full extraction

### Streaming Copy
- Buffered async copy (64KB-2MB chunks depending on file size)
- SelectBufferSize() method for intelligent buffer selection

### Smart Retry Logic
- Retries ONLY if stream.CanSeek == true
- Non-seekable streams fail fast (one attempt)
- Exponential backoff preserved for seekable sources (50ms→100ms→200ms)

## Safety Guarantees (Unchanged)

✅ **Rollback Correctness**
- LIFO stack-based deletion order
- Deterministic cleanup on any failure

✅ **Cancellation Safety**
- Full cleanup of partial writes
- CancellationToken respected at operation boundaries

✅ **Path Safety**
- mods/ directory enforcement
- PathSafety.GetSafePath() validates all extractions

✅ **No Orphan Files**
- File tracked before write attempt
- Rollback removes any incomplete writes

✅ **InstallResult Contract**
- Success, IsPartial, FilesWritten unchanged
- Error messages propagated correctly

## Testing

### Test Coverage
- 149 total tests (140 existing + 9 new streaming tests)
- All passing
- Zero regressions from Phase B++

### Scenarios Validated
- Small file streaming
- Large file streaming (200MB+)
- Seekable stream retry behavior
- Non-seekable stream fail-fast
- Partial write rollback
- Cancellation mid-stream cleanup

## Production Observability

### Enhanced Application Logging
- Version tag (1.4.0) included in every log entry for release correlation
- 8-character session ID enables multi-log aggregation per installation session
- Full ISO 8601 timestamps (date + time) for accurate event sequencing
- Stack traces captured for exceptions, not just messages

### Structured Operation Logging
- InstallQueue: `INSTALL_START` / `INSTALL_SUCCESS` / `INSTALL_FAILED` lifecycle tracking
- FileInstaller: `EXTRACT_START` (with file size and buffer strategy) and `EXTRACT_OK` status
- OpenIvExecutor: `COPY_START` (with stream seekability), `COPY_RETRY` (with backoff timing), `COPY_FAILED` outcomes
- Rollback system: `ROLLBACK_START`, `ROLLBACK_DELETE` (per file), `ROLLBACK_COMPLETE` summary
- XML patches: `PATCH_APPLY` and `PATCH_OK` markers for config modifications

**Impact**: Enables grep-based monitoring and log aggregation for production incident diagnosis. Retry and rollback behavior fully visible. Session ID allows tracking multi-entry sequences.

## Breaking Changes

**None**. This is an internal architecture improvement:
- Existing mod installation flows unchanged
- No API contract changes
- All existing tests pass without modification

## Technical Notes

### Stream Lifetime Management
- Archive lifetime controlled by using statements
- Streams consumed within try-using blocks
- No orphaned file handles or stream leaks

### Non-Seekable Stream Handling
- Compressed archive streams (RAR, 7Z) may be non-seekable
- Phase C smart retry detects and respects this
- Single attempt, fail-fast on non-seekable + write error

### Memory Safety
- Buffers are fixed allocations (not cumulative)
- One buffer active per file copy
- No dynamic memory spikes

## Deployment

### Compatibility
- Requires .NET 8.0 (unchanged)
- Windows x64 (unchanged)
- No breaking changes to installer logic

### Migration
- Existing mods continue to work
- No user action required
- Automatic benefit from streaming optimization

## What's Next

1. **Production Monitoring** — Observe crash.log for edge cases
2. **Conflict Resolution UI** — User-facing improvements for duplicate mods
3. **Optional**: OpenIV Export integration
4. **Optional**: Phase D (policy enforcement layer)

---

**Status**: Ready for production.
**Build**: 0 errors, 0 critical warnings.
**Tests**: 149/149 passing.
**Streaming**: Fully implemented and validated.
