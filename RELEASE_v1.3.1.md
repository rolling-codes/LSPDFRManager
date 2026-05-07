# Release v1.3.1

## Summary

Phase B++ real-world validation complete. Full test coverage of installer under realistic failure conditions.

## What's New

### Real-World Validation Suite
- **140 total tests** (131 existing + 9 new real-world scenarios)
- Comprehensive stress testing across failure modes:
  - Large archive handling (200MB+ files)
  - Deep nesting paths (8+ directory levels)
  - Locked file retry + rollback behavior
  - Corrupt archive fail-fast + cleanup
  - Duplicate file stress (100-file extraction)
  - Partial overwrite scenarios
  - Non-seekable stream error handling
  - Cancellation mid-install with full rollback
  - Type consistency validation

### Verified Guarantees

✅ **Rollback Correctness**
- LIFO stack-based deletion order
- Deterministic, predictable cleanup
- No orphan files on any failure

✅ **Retry Resilience**
- Exponential backoff (50ms → 100ms → 200ms)
- Seekability detection prevents infinite loops
- Fail-fast on unrecoverable errors

✅ **Cancellation Safety**
- Immediate response to CancellationToken
- Rollback all written files
- Zero partial installs

✅ **Path Safety**
- All extractions confined to `mods/` directory
- Traversal attacks blocked at validation boundary
- Pre-flight disk space checks

✅ **Error Visibility**
- All failures logged with actionable context
- Crash logs capture error origin
- No silent partial installs

## Technical Details

### Test Architecture
- FakeArchive infrastructure for deterministic testing
- No real ZIP/RAR files in test suite
- Direct Executor calls with controlled failure injection
- Orphan file verification on all failure paths

### Pipeline Validation
- Analyzer → Planner → Validator → Executor
- Integration tests confirm contract boundaries
- Unit tests verify individual components

## Breaking Changes

None. This is test-only. No installer logic changes.

## Installation

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Next Steps

- **Phase C (Streaming)**: Optimize large archive extraction
- **Conflict Resolution**: UI for handling duplicate mods
- **OpenIV Integration**: Direct export from game files

## Known Limitations

- No password-protected archive support
- Concurrent extraction (single-threaded queue only)
- Limited to GTA V file paths

---

**Status**: Ready for production use.
**Tests**: 140/140 passing.
**Build**: 0 errors, 0 critical warnings.
