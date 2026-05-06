# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Build (Debug)**
```bash
dotnet build LSPDFRManager.sln
```

**Run (requires GTA V path configured in settings)**
```bash
dotnet run --project LSPDFRManager.csproj
```

**Build self-contained executable (Release)**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

**Build release ZIP (framework-dependent)**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.2.1 -p:DebugType=None -p:DebugSymbols=false
Compress-Archive -Path publish/v3.2.1/* -DestinationPath LSPDFRManager-v3.2.1-win-x64.zip
```

## Testing

**Run all tests**
```bash
dotnet test
```

**Run single test file**
```bash
dotnet test --filter "ClassName"
```

**Run with verbose output**
```bash
dotnet test -v detailed
```

## Architecture

**MVVM WPF Desktop App** (.NET 8, Windows x64 only)

### Layer Structure
- **Views/** — WPF UserControls, one per tab (Library, Install, Settings, Browse, Config)
- **ViewModels/** — MVVM view models; MainViewModel orchestrates tab navigation; each tab has a dedicated VM
- **Domain/** — Data classes (InstalledMod, ModInfo, ModType, AppConfig, ModManifest)
- **Services/** — Business logic (ModLibraryService, ModDetector, FileInstaller, ConfigManagerService, BackupService, etc.)
- **Core/** — AppLogger (file logging), InstallQueue (background async install processor)
- **Converters/** — WPF value converters (e.g., InverseBoolConverter, StringToBrushConverter)

### Repository References
- Repository: https://github.com/rolling-codes/LSPDFRManager
- Current release notes: [RELEASE_v3.2.1.md](RELEASE_v3.2.1.md)
- Desktop app project: [LSPDFRManager.csproj](LSPDFRManager.csproj)
- Solution: [LSPDFRManager.sln](LSPDFRManager.sln)

### Key Singletons
- **ModLibraryService** — In-memory registry of all installed mods, synced to `library.json`. Search, enable/disable, conflict detection.
- **AppConfig** — App settings (GTA path, backup folder, etc.), synced to `config.json`.
- **LspdfrStatusService** — Monitors live status of LSPDFR/GTA V processes (shown in sidebar).
- **InstallQueue** — Background task processor; installs one mod at a time to avoid file conflicts.

### Core Workflows

**Install Flow**
1. User drag-drop or browse archive → InstallView / InstallViewModel
2. ModDetector runs detection (file paths, extensions, archive name keywords) → confidence score
3. User confirms type + author → FileInstaller extracts to GTA path, snapshots file system
4. InstallQueue enqueues install, processes asynchronously
5. Installed files tracked in InstalledMod → ModLibraryService saves to library.json

**Enable/Disable**
- Rename installed files with `.disabled` suffix (e.g., `plugin.dll` → `plugin.dll.disabled`)
- GTA V/LSPDFR ignore `.disabled` files
- Toggled via ModLibraryService.SetEnabled()

**Detection Scoring**
- ModDetector analyzes path patterns, file extensions, archive names
- Scores each ModType (LSPDFR Plugin, DLC, Replace, ASI, Script, EUP, Map, Sound) with confidence 0–100
- Low confidence warns user before install

**Backup/Restore**
- BackupService ZIPs library.json, configs.json, key file copies
- RestoreService extracts snapshot, restores app state

### Storage

All data → `%APPDATA%\LSPDFRManager\`:
- `library.json` — Installed mods registry
- `configs.json` — Captured mod config snapshots
- `config.json` — App settings
- `keys/` — Cached key files
- `Backups/` — ZIP archives
- `app.log` — Runtime log

### Testing

**Test Projects**
- `LSPDFRManager.Tests/` — xUnit suite (AppConfigTests, ModDetectorTests, ModLibraryServiceTests, etc.)
- **No mocking of file I/O** — tests create real temp folders / files
- **No mocking of ModLibraryService** — tests use real JSON serialization

**Patterns**
- Tests exercise real filesystem ops (temp directories, mod extraction)
- AppConfig, ModLibraryService are singletons; tests reset Instance before/after
- No database; JSON file-based storage simplifies testing

## Important Patterns

**Nullable Reference Types**
- `<Nullable>enable</Nullable>` in .csproj
- All code must declare intent: `string?` for nullable, `string` for non-null

**Implicit Usings**
- `<ImplicitUsings>enable</ImplicitUsings>` in .csproj
- Common namespaces auto-imported (System, System.Collections.Generic, etc.)
- File-scoped namespaces: `namespace LSPDFRManager.ViewModels;` (no braces)

**Global Usings**
- [GlobalUsings.cs](GlobalUsings.cs) — common cross-cutting imports

**MVVM Foundation**
- [ObservableObject.cs](ViewModels/ObservableObject.cs) — base class for VMs, implements INotifyPropertyChanged
- [RelayCommand.cs](ViewModels/RelayCommand.cs) — simple ICommand for button actions

**Dark Theme**
- [Resources/Styles.xaml](Resources/Styles.xaml) — centralized dark theme (brushes, templates)

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| SharpCompress | 0.38.0 | RAR, 7-Zip archive extraction (ZIP built-in) |
| Microsoft.NET.Test.Sdk | 17.10.0 | Test framework |
| xUnit | 2.9.0 | Unit testing |

## Gotchas

**Singleton Thread Safety**
- ModLibraryService, AppConfig, LspdfrStatusService use lazy-init singletons
- Modifications (Add, Remove, SetEnabled) trigger immediate Save to JSON
- No locks; assume single-threaded UI access

**File Locking**
- On enable/disable, renames files on disk while GTA V may have them open
- May fail silently if GTA V holds file handle
- Check app.log for rename errors

**Archive Extraction**
- SharpCompress extracts RAR/7-Zip; ZipFile (System.IO.Compression) for ZIP
- Duplicate file paths in archive (rare) — only first is extracted
- No password-protected archives support

**Mod Detection Confidence**
- Low confidence (< 50) shows warning; user can override
- Keywords in archive name can boost/flip detection (e.g., "dlcpack" → DLC)
- Path patterns checked recursively (e.g., `plugins/lspdfr/` at any depth)

## Installer Safety & Testing (Phase A/B Hardening)

**Core Invariant**

The installer must NEVER leave the filesystem in a partially-installed state. All design decisions, refactors, and optimizations must preserve this invariant.

**Extraction Safety Contract (Do Not Violate)**

All archive extraction MUST follow this sequence:

1. Resolve path via PathSafety.GetSafePath()
2. Create directory (if needed)
3. Copy stream → file
4. Add file to rollback list ONLY after successful copy

Any deviation risks path traversal vulnerabilities or partial installs (data corruption).

**Rollback Guarantee**

On ANY failure during install:

- Zero files from the attempted install may remain on disk
- No partial directories should persist
- System must return to pre-install state

This is a core invariant. All changes must preserve it.

**Deterministic Failure Testing (Required)**

All installer logic must be testable using fake archives.

Use:
- FakeArchive
- FakeArchiveEntry
- ThrowingStream (for mid-stream failures)

Do NOT rely on:
- Real ZIP files for unit tests
- OS-level behavior for correctness validation

Real archives are for Phase B only (validation, not logic correctness).

**Phase Gates (Strict)**

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

**Archive Adapter Boundary**

External libraries (e.g., SharpCompress) MUST be isolated behind IArchive/IArchiveEntry.

Rules:
- No SharpCompress types outside adapter layer
- Installer logic must not depend on library-specific behavior
- All adapters must conform to same contract (including streaming)

Reason:
- Enables testing (FakeArchive)
- Prevents vendor lock-in
- Allows safe refactoring

**Failure Visibility (Required)**

All install failures MUST be visible in UI:

- Global error surface (banner/toast)
- Per-item error state (ModCard)

Logs are supplemental only. A failure that is not visible in UI is considered a bug.

**Async / Streaming Guardrails**

When introducing async or streaming:

- Do not change rollback ordering
- Do not move PathSafety validation
- Do not mix sync/async incorrectly (no Task.Run for I/O)
- Always propagate CancellationToken
- Ensure UI remains responsive (yield if needed)

All existing tests MUST pass unchanged after refactor.

**Anti-Patterns (Do Not Do)**

- ❌ Writing files without PathSafety.GetSafePath()
- ❌ Adding files to rollback list before successful write
- ❌ Catching and swallowing install exceptions
- ❌ Relying only on logs for error reporting
- ❌ Testing installer logic only with real archives
- ❌ Introducing performance optimizations before Phase B validation

## Common Tasks

**Add a new mod type**
- Add entry to ModType enum [Domain/ModType.cs](Domain/ModType.cs)
- Update ModDetector detection logic [Services/ModDetector.cs](Services/ModDetector.cs)
- Add UI for type filtering in LibraryView/ViewModel (search/filter dropdown)

**Add app setting**
- Add property to AppConfig [Domain/AppConfig.cs](Domain/AppConfig.cs)
- Update config.json schema
- Wire UI control in SettingsView/SettingsViewModel

**Debug install failures**
- Check app.log for error message
- InstallQueue.ProcessLoop logs InstallStarted/InstallFailed events
- Verify GTA path is valid and writable
- Confirm archive is not corrupted (SharpCompress extraction)

**Profile performance**
- Enable verbose logging in AppLogger.cs (all Service methods log entry/exit)
- Check backup/restore speed (large libraries = slow JSON I/O)
- Archive extraction bottleneck for large mods (SharpCompress perf)
