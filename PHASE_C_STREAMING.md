# Phase C: Streaming Optimization Design

## Status: DESIGN ONLY (Not for Implementation Yet)

Current architecture is **already streaming**. Phase C optimizes buffer strategy.

## Key Findings

### What Works
- Archive → Stream → Disk (direct, no temp files)
- IArchive.Entries exposes OpenEntryStream()
- Both FileInstaller and OpenIvExecutor use streams

### What to Optimize
- Buffer size: fixed 81920 bytes
- Non-seekable streams: wasted retries
- Large files: inefficient I/O

## Three-Tier Buffer Strategy

```
< 1MB   : 64KB  (small)
1-100MB : 512KB (medium)
> 100MB : 2MB   (large)
```

## Smart Retry Detection

Only retry if source.CanSeek == true. Fail fast on non-seekable.

## Invariants Preserved

✅ LIFO rollback (unchanged)
✅ Cancellation safety (unchanged)
✅ Path safety (unchanged)
✅ Retry behavior (refined, not broken)

## What Doesn't Change

- InstallResult contract
- Archive adapter interfaces
- Executor public API
- Rollback logic
- Cancellation handling

## Implementation Plan (Future)

PR 1: SelectBufferSize() function
PR 2: Smart retry (CanSeek detection)
PR 3: Adapter validation
PR 4: Benchmarks (optional)

## Zero Risk

No breaking changes. All existing tests pass. Executor logic untouched.
