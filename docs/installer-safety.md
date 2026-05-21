# Installer Safety & Testing (Phase A/B Hardening)

## Core Invariant

The installer must NEVER leave the filesystem in a partially-installed state. All design decisions, refactors, and optimizations must preserve this invariant.

## Extraction Safety Contract (Do Not Violate)

All archive extraction MUST follow this sequence:

1. Resolve path via PathSafety.GetSafePath()
2. Create directory (if needed)
3. Copy stream → file
4. Add file to rollback list ONLY after successful copy

Any deviation risks path traversal vulnerabilities or partial installs (data corruption).

## Rollback Guarantee

On ANY failure during install:

- Zero files from the attempted install may remain on disk
- No partial directories should persist
- System must return to pre-install state

## Deterministic Failure Testing (Required)

All installer logic must be testable using fake archives.

Use:
- `FakeArchive`
- `FakeArchiveEntry`
- `ThrowingStream` (for mid-stream failures)

Do NOT rely on real ZIP files for unit tests or OS-level behavior for correctness validation. Real archives are for Phase B only.

## Phase Gates (Strict)

Phase A (Core correctness) MUST be complete before:
- UI work
- Performance optimization
- Feature expansion

Phase B (Manual validation) MUST pass before:
- Refactoring installer internals
- Changing archive handling

Phase C (Optimization) MUST NOT:
- Change rollback behavior
- Bypass PathSafety
- Alter InstallResult contract

## Archive Adapter Boundary

External libraries (e.g., SharpCompress) MUST be isolated behind IArchive/IArchiveEntry.

- No SharpCompress types outside adapter layer
- Installer logic must not depend on library-specific behavior
- All adapters must conform to same contract (including streaming)

This enables testing via FakeArchive, prevents vendor lock-in, and allows safe refactoring.

## Failure Visibility (Required)

All install failures MUST be visible in UI:

- Global error surface (banner/toast)
- Per-item error state (ModCard)

Logs are supplemental only. A failure that is not visible in UI is considered a bug.

## Async / Streaming Guardrails

When introducing async or streaming:

- Do not change rollback ordering
- Do not move PathSafety validation
- Do not mix sync/async incorrectly (no Task.Run for I/O)
- Always propagate CancellationToken
- Ensure UI remains responsive (yield if needed)

All existing tests MUST pass unchanged after refactor.

## Anti-Patterns (Do Not Do)

- ❌ Writing files without PathSafety.GetSafePath()
- ❌ Adding files to rollback list before successful write
- ❌ Catching and swallowing install exceptions
- ❌ Relying only on logs for error reporting
- ❌ Testing installer logic only with real archives
- ❌ Introducing performance optimizations before Phase B validation
