# Release v3.7.7 — Installer safety, rollback reliability, and mod intelligence

## Installer safety and review improvements

- Added pre-install review screen before any filesystem mutation. Entries are grouped into new files, overwrites, suspicious entries, blocked entries, and junk entries.
- Added junk-entry detection: macOS metadata (`__MACOSX/`, `._*`, `.ds_store`), temp/log/cache folders, and `.bak`/`.tmp`/`.log` extensions are excluded from extraction automatically.
- Added distinct post-install visual states: no-mutation info pill, partial-rollback warning, clean success, and hard failure — replacing the single generic red error pill.
- Surfaced rollback failures explicitly in install results and logs.

## Transactional rollback hardening

- Added `TransactionService` — persists a record of every file written and overwritten during install, so rollback can remove added files and restore overwritten originals from backup.
- `FileInstaller` now records the transaction log before touching any file and commits it atomically after a successful install.
- `InstallQueue` propagates rollback failures into `InstallResult` for UI visibility.

## Mod type detection

- Added `IModTypeDetectionService` — pure, evidence-based classifier: archive entry paths in, `ModTypeDetectionResult` out. No file I/O.
- Detects: ASI mods, ScriptHookVDotNet scripts, OIV packages, DLC packs, LSPDFR plugins, EUP clothing packs, maps/MLOs, sound packs, and config-only archives.
- Handles nested archive roots, mixed archives, low-confidence/ambiguous inputs, and unknown archives gracefully.
- Secondary types tracked above threshold; `IsMixed` set when primary/secondary confidence gap is narrow.

## Dependency warnings

- Added `IDependencyDetectionService` — pure catalog-driven mapper from `ModTypeDetectionResult` to `DependencyWarning` list.
- Deduplicates warnings across mixed archives (e.g. ASI + Script both require Script Hook V — emitted once).
- Dependency warnings appear in the pre-install review panel.
- Mappings: Script → Script Hook V + ScriptHookVDotNet; ASI → Script Hook V + ASI Loader; LSPDFR Plugin → LSPDFR + RAGE Plugin Hook; OIV → OpenIV; EUP → EUP Menu.
- All severities are warnings — no blocking without a live install probe.

## Verification

- 617/617 tests passing.
- Clean build.

## Issues closed

- #23 Pre-install review screen
- #24 Transactional rollback hardening
- #25 Mod type detection service
- #26 Dependency detection and pre-install warnings
- #27 Install result UX polish — distinct visual states
