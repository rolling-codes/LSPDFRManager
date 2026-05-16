# LSPDFR Manager v3.7.5

This release introduces the **LSPDFR Setup Stabilizer** feature set. Theme: _Install safer. Diagnose faster. Get back on patrol._

## Highlights

- **Setup Doctor** — automated health scan of your GTA V / LSPDFR / RPH installation, surfacing missing files, wrong locations, keybind conflicts, version drift, and write permission issues.
- **Recipe Validator** — checks that required files for ELS, Ultimate Backup, LSPDFR, and other plugins are present in the correct locations.
- **Config Discovery & INI Parser** — discovers all `.ini` and `.xml` configs under the GTA root and applies safe, backup-first, preview-before-apply patches.
- **Keybind Conflict Scanner** — detects duplicate keybinds across all plugin INI configs and surfaces conflicts with severity grading.
- **Patrol Setup Presets** — one-click presets for controller and keyboard/mouse setups (e.g. clearing BackupMenu conflicts for Ultimate Backup controller use).
- **Backup Easy Editor** — validates Ultimate Backup XML configs and provides a safe preview of uniform patch operations. All edit paths are backup-first and preview-only until confirmed.

## Diagnostics

- `SetupDoctorService` runs as part of `DiagnosticsOrchestrator` — all findings appear in the Diagnostics panel alongside existing plugin health, dependency, and conflict scans.
- Findings are deduplicated by Title + Path + Severity and normalized to Confidence 1.0.
- HTML diagnostic export now correctly HTML-encodes all finding fields.

## Safety

- All INI edits: backup created once on first write; subsequent writes to the same file do not overwrite the original backup.
- All Backup XML operations: preview-only until user confirms; original file never modified without a `.bak` copy.
- `BackupXmlParser` handles access-denied, malformed XML, empty files, and unknown structures gracefully — no crashes.

## Version Consistency

- Updated project versioning to `3.7.5` in `LSPDFRManager.csproj`.

## Validation

- `dotnet build LSPDFRManager.sln` — 0 errors
- `dotnet test` — 353/353 passing

## Known Non-Blockers

- Crash Timeline Analyzer: domain models shipped; service implementation deferred.
- Support Bundle Export, RPH Startup Optimizer, PatrolSnapshot: deferred to next release.
- UI panels for Setup Doctor / Presets / Backup Easy Editor: deferred; features are fully testable via services.
- XML keybind scanning (ELS XML): deferred.

## Download

- `LSPDFRManager-v3.7.5-win-x64.zip`
