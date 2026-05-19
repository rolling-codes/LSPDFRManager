# LSPDFR Manager — React Migration Analysis

**Generated:** 2026-05-19  
**Repo:** https://github.com/rolling-codes/LSPDFRManager  
**Current version:** v3.7.17  
**Branch:** main

---

## 1. Current Architecture Summary

LSPDFR Manager is a **Windows-only .NET 8 WPF desktop application** (`OutputType=WinExe`, `net8.0-windows`, `UseWPF=true`). It manages GTA V / LSPDFR mod installations on the local filesystem.

### Projects in the solution

| Project | SDK | Framework | Purpose |
|---------|-----|-----------|---------|
| `LSPDFRManager.csproj` | `Microsoft.NET.Sdk` | `net8.0-windows` | Main WPF desktop application |
| `LSPDFRManager.Api/` | `Microsoft.NET.Sdk.Web` | `net8.0` | Scraper-only ASP.NET Core Minimal API (lcpdfr.com) |
| `LSPDFRManager.Tests/` | xUnit | `net8.0-windows` | Test suite (878 tests) |

### Architecture pattern

- **MVVM** — Views bind to ViewModels via WPF data binding; no DI container; plain singletons via `Instance` pattern.
- **No repository layer** — Services operate directly on the filesystem.
- **No database** — All persistence is JSON files under `%APPDATA%\LSPDFRManager\`.
- **Layering:** `Views → ViewModels → Services/Core → Domain`

### Key packages

| Package | Version | Purpose |
|---------|---------|---------|
| `SharpCompress` | 0.38.0 | Archive extraction (.zip/.rar/.7z) |
| `Microsoft.Web.WebView2` | 1.0.2739.15 | Chromium browser control (Browse tab) |
| `HtmlAgilityPack` | 1.11.62 | HTML scraping in `LSPDFRManager.Api` |

---

## 2. Current Build / Test / Lint Commands

```powershell
# Restore
dotnet restore LSPDFRManager.sln

# Build (all projects)
dotnet build LSPDFRManager.sln --configuration Release

# Tests
dotnet test LSPDFRManager.Tests\LSPDFRManager.Tests.csproj --configuration Release

# Publish (self-contained=false, win-x64)
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish -p:DebugType=None -p:DebugSymbols=false
```

No frontend build commands currently exist. No JavaScript/TypeScript in the repo today.

---

## 3. Current App Features by Tab / View

All 17 views are smoke-tested in `NavigationSmokeTests.cs`.

| # | View | ViewModel | Feature Summary |
|---|------|-----------|-----------------|
| 1 | `DashboardView` | `DashboardViewModel` | Status overview, compatibility matrix, telemetry, LSPDFR readiness |
| 2 | `InstallView` | `InstallViewModel` | Select + install mods from archive/folder; conflict detection; preview |
| 3 | `LibraryView` | `LibraryViewModel` | Enable/disable installed mods; search; bulk toggle; export |
| 4 | `BrowseView` | `BrowseViewModel` | Browse lcpdfr.com via WebView2 + API scraper; queue downloads |
| 5 | `BackupsView` | `BackupsViewModel` | Create, restore, and delete backup ZIPs |
| 6 | `ConfigView` | `ConfigViewModel` | Edit mod config files (.ini/.xml); preview patches; backup-before-save |
| 7 | `DiagnosticsView` | `DiagnosticsViewModel` | Run diagnostics orchestrator; view HTML report |
| 8 | `HistoryView` | `HistoryViewModel` | View change history of installs/uninstalls |
| 9 | `ProfilesView` | `ProfilesViewModel` | Create/switch/delete mod profiles (load sets) |
| 10 | `SettingsView` | `SettingsViewModel` | GTA path, app config, update check, feature flags |
| 11 | `LogViewerView` | `LogViewerViewModel` | View app.log in-app |
| 12 | `SafeModeView` | `SafeModeViewModel` | Emergency safe-mode disable of mods for crash recovery |
| 13 | `DevDiagnosticsView` | `DevDiagnosticsViewModel` | Developer/internal diagnostics |
| 14 | `OivView` | `OivViewModel` | Create and install OpenIV (.oiv) packages |
| 15 | `CleanupView` | `CleanupViewModel` | Safe LSPDFR cleanup/reinstall helper (4-screen wizard) |
| 16 | `PatrolReadinessDashboardView` | `PatrolReadinessDashboardViewModel` | Patrol readiness check and health summary |
| 17 | `SetupWizardView` | `SetupWizardViewModel` | First-launch wizard: detect GTA path, validate LSPDFR install |

---

## 4. Existing WPF Views and Their ViewModels

### Shared infrastructure

| File | Role |
|------|------|
| `ViewModels/ObservableObject.cs` | Base class: `INotifyPropertyChanged` |
| `ViewModels/RelayCommand.cs` | ICommand implementation |
| `Core/Commands/AsyncAppCommand.cs` | Async ICommand |
| `Core/UiDispatcher.cs` | Marshals background work to UI thread |
| `Core/InstallQueue.cs` | Serializes concurrent install operations |
| `Core/AppLogger.cs` | Structured file logging |

### Views/Components sub-folder

`Views/Components/` contains reusable XAML user controls shared across multiple views.

---

## 5. Existing Domain / Service / Core Responsibilities

### Core/ — install engine

| File | Responsibility |
|------|---------------|
| `Core/CarInstall/OpenIvExecutor.cs` | Orchestrates OpenIV-style installs; calls XmlPatcher |
| `Core/CarInstall/XmlPatcher.cs` | Applies XML patch operations (vehicles.meta, etc.) |
| `Core/CarInstall/OpenIvInstallPlanner.cs` | Builds install plan from .oiv manifest |
| `Core/CarInstall/DiskSpaceValidator.cs` | Checks available disk space before install |
| `Core/AppLogger.cs` | App-wide file logger |
| `Core/InstallQueue.cs` | Single-file queue for install serialization |
| `Core/UiDispatcher.cs` | UI thread dispatcher abstraction |

### Services/ — 90+ files, key safety-sensitive ones

| File | Responsibility |
|------|---------------|
| `PathSafety.cs` | **CRITICAL** — path traversal prevention for all file writes |
| `FileInstaller.cs` | **CRITICAL** — archive extraction, rollback on failure, path safety |
| `InstallerSafetyPolicy.cs` | Policy decisions about overwrite risks |
| `TransactionService.cs` | Install transaction tracking |
| `RestorePointService.cs` | Create/restore restore points |
| `BackupService.cs` | ZIP-based backup creation and restoration |
| `ProfileManager.cs` | Mod profile (load set) management |
| `ModLibraryService.cs` | Track installed mods, enable/disable |
| `ModConflictDetector.cs` | Pre-install conflict detection |
| `JsonFileStore.cs` | Generic JSON persistence |
| `AppDataPaths.cs` | All `%APPDATA%\LSPDFRManager\` paths |
| `GtaFileBackupService.cs` | Cleanup backup ZIPs before deletion |
| `CleanupApplyService.cs` | Backup → delete → verify cleanup flow |
| `LspdfrCleanupScanner.cs` | Classifies LSPDFR candidates for cleanup |
| `DiagnosticsOrchestrator.cs` | Runs all diagnostic scanners, produces HTML report |
| `GamePathDetector.cs` | Detects GTA V installation path |
| `LspdfrInstallLocator.cs` | Locates and validates LSPDFR installation |
| `VersionDetectorService.cs` | Reads GTA5.exe/DLL versions + SHA-256 hashes |
| `GtaBaselineService.cs` | Persists GTA exe baseline for drift detection |
| `GtaDriftDetector.cs` | Compares current vs baseline GTA exe state |
| `IniParser.cs` | Line-based INI patcher; backup-first |
| `ConfigDiscoveryService.cs` | Discovers .ini/.xml configs under GTA root |
| `BackupEasyEditorService.cs` | Preview/patch Ultimate Backup XML; never writes without confirm |
| `SetupDoctorService.cs` | Post-install health validation |
| `SetupWizardService.cs` | First-launch GTA directory scan |
| `SmartInstallPlanner.cs` | Analyzes install conflicts before commit |
| `EmergencyRecoveryService.cs` | Safe-mode emergency mod disable |

### Domain/ — 80+ model files

Pure data records with no I/O. Key types:

| Type | Role |
|------|------|
| `ModInfo` | Source mod descriptor (path, name, type) |
| `InstallResult` | Install outcome (success/failure/rollback detail) |
| `InstalledMod` | Persisted record of an installed mod |
| `AppConfig` | User configuration (GTA path, settings) |
| `RestorePoint` / `RestorePointEntry` | Restore point snapshot |
| `BackupManifest` | Backup catalog |
| `ModProfile` | Named mod load set |
| `CleanupScanResult` / `RemovalCandidate` | Cleanup scanner output |
| `VersionBundle` | Detected versions + hashes for all key executables |
| `GtaBaseline` | Persisted GTA exe state snapshot |

### Features/ — feature-module pattern

| Feature | Files | Role |
|---------|-------|------|
| Install | `InstallWorkflowController`, `InstallFeatureModule` | Install workflow orchestration |
| Library | `LibraryWorkflowController` | Enable/disable bulk operations |
| Updates | `UpdateWorkflowController`, `UpdateFeatureModule` | Mod update flow |
| OivCreatorTemplates | `OivTemplateController` | OIV template application |
| PatrolReadiness | `PatrolReadinessController` | Readiness check orchestration |
| SafeMode | `SafeModeController` | Emergency recovery orchestration |

---

## 6. Existing API Capabilities

`LSPDFRManager.Api` is a **scraper-only** ASP.NET Core Minimal API. It does **not** expose any local mod management operations.

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/health` | GET | Health check |
| `/api/mods/search?q=&category=` | GET | Searches lcpdfr.com for mods |
| `/api/mods/{id}` | GET | Fetches mod detail page from lcpdfr.com |

The WPF app starts this API as a local process and communicates via `BrowseApiServiceManager`. The Browse tab's WebView2 control uses this API as a proxy to scrape lcpdfr.com.

**There is no existing local-management REST API.** All mod management, install, backup, profile, and diagnostics operations are performed by the WPF app calling C# services directly.

---

## 7. Existing Data Persistence Formats and Storage Locations

All data lives under `%APPDATA%\LSPDFRManager\` (Windows only).

| File path | Type | Contents |
|-----------|------|----------|
| `config.json` | JSON | `AppConfig` — GTA path, settings, feature flags |
| `library.json` | JSON | List of `InstalledMod` records |
| `configs.json` | JSON | Saved mod config snapshots |
| `app.log` | Plain text | App log |
| `logs/browse_api_service.log` | Plain text | Browse API service log |
| `logs/errors.log` | Plain text | Error log |
| `data/change_history.json` | JSON | `ChangeHistoryEntry[]` |
| `data/mod_metadata.json` | JSON | `ModMetadata[]` |
| `data/backup_manifest.json` | JSON | `BackupManifest` |
| `profiles/` | Directory | One JSON file per `ModProfile` |
| `restore_points/index.json` | JSON | Restore point index |
| `restore_points/*/` | Directories | Restore point file snapshots |
| `gta_baseline.json` | JSON | `GtaBaseline` — GTA exe hash/size/version snapshot |

`JsonFileStore<T>` handles all reads/writes with UTF-8 JSON serialization (System.Text.Json).

---

## 8. Critical Safety-Sensitive Services

These must **never** be weakened, bypassed, or re-implemented without equivalent tests:

| Service | Invariant |
|---------|-----------|
| `PathSafety.GetSafePath()` | Required before **every** file write; throws on path traversal |
| `FileInstaller` | Rollback on any failure; file added to rollback list **only after** successful copy |
| `InstallerSafetyPolicy` | Controls overwrite policy decisions |
| `TransactionService` | Tracks install transaction state |
| `GtaFileBackupService` | Backup ZIP must succeed before any cleanup deletion |
| `CleanupApplyService` | Backup → delete → verify; abort on backup failure |
| `BackupEasyEditorService` | Preview-before-apply; never writes XML without user confirmation |
| `IniParser` | Backup-first; original `.bak` never overwritten |
| `RestorePointService` | Point-in-time snapshots must be atomic |
| `BackupService` | ZIP backup integrity must be verifiable |

---

## 9. WPF View / ViewModel → Proposed React Route / Component Mapping

| WPF View | Proposed React Route | Notes |
|----------|---------------------|-------|
| `SetupWizardView` | `/setup` (modal / redirect) | First-launch only; multi-step wizard |
| `DashboardView` | `/` or `/dashboard` | Status overview; read-heavy |
| `InstallView` | `/install` | Needs file picker (OS native) |
| `LibraryView` | `/library` | Enable/disable; search; bulk ops |
| `BrowseView` | `/browse` | Requires WebView2 or iframe; complex |
| `BackupsView` | `/backups` | CRUD for backup ZIPs |
| `ConfigView` | `/config` | INI/XML editor; preview pane |
| `DiagnosticsView` | `/diagnostics` | HTML report rendering |
| `HistoryView` | `/history` | Read-only log table |
| `ProfilesView` | `/profiles` | Profile CRUD |
| `SettingsView` | `/settings` | Config form |
| `LogViewerView` | `/logs` | Log file viewer |
| `SafeModeView` | `/safe-mode` | Emergency recovery; confirm-gated |
| `DevDiagnosticsView` | `/dev-diagnostics` | Internal only |
| `OivView` | `/oiv` | OIV creator/installer wizard |
| `CleanupView` | `/cleanup` | 4-step cleanup wizard |
| `PatrolReadinessDashboardView` | `/patrol-readiness` | Readiness check + summary |

---

## 10. Backend / Service APIs Needed for Each React Screen

A **new local REST API layer** must be added to `LSPDFRManager.Api` (or a new project) to expose existing C# service operations to the React frontend.

| Screen | Required API Operations |
|--------|------------------------|
| Dashboard | GET readiness status, GET compatibility bundle, GET drift warnings |
| Install | POST install (source path, GTA path), GET install progress, GET install conflicts |
| Library | GET installed mods, POST enable/disable mod, DELETE mod (uninstall), GET search |
| Browse | GET `/api/mods/search`, GET `/api/mods/{id}` (already exists) |
| Backups | GET backups, POST create backup, POST restore backup, DELETE backup |
| Config | GET discovered configs, GET config preview, POST apply patch |
| Diagnostics | POST run diagnostics, GET diagnostics report |
| History | GET change history |
| Profiles | GET profiles, POST create profile, POST switch profile, DELETE profile |
| Settings | GET config, PUT config, POST validate GTA path |
| Logs | GET log contents (paginated) |
| Safe Mode | GET safe mode plan, POST apply safe mode, POST restore from safe mode |
| OIV | POST create oiv, POST install oiv, GET oiv inspection |
| Cleanup | GET cleanup scan, POST apply cleanup mode |
| Patrol Readiness | GET patrol readiness result |
| Setup Wizard | GET scan result for directory, POST save GTA path |

---

## 11. Features That Cannot Be Safely Implemented in Browser-Only React

The following require local Windows capabilities that a browser tab cannot provide:

| Feature | Reason |
|---------|--------|
| File install / extraction | Requires filesystem write access; archives decoded by SharpCompress in C# |
| Path traversal protection | `PathSafety.GetSafePath()` is server-side; cannot be replicated safely in JS |
| Archive reading (.rar/.7z/.zip) | SharpCompress handles formats not available in the browser |
| Rollback on failure | Requires atomic temp-file-then-move operations in C# |
| Backup ZIP creation | Reads and writes GTA directory; must run server-side |
| INI/XML patching | File system operations; backup-first invariant requires server-side control |
| Restore points | Filesystem snapshots; must be server-side |
| Profile switching | Reads/writes mod library and GTA files |
| Diagnostics (full scan) | Reads GTA directory, RPH logs, LSPDFR files |
| Crash log analysis | Reads local log files |
| Safe mode recovery | Writes to GTA directory to disable mods |
| Cleanup / deletion | Deletes from GTA directory; requires backup first |
| WebView2 (Browse tab) | Requires Chromium embedded in the desktop shell |
| Native file picker | `OpenFileDialog` is OS-native; browser `<input type=file>` has restrictions |
| Auto-start / process management | API must be started as a local process |
| `%APPDATA%` persistence | Browser storage (IndexedDB/localStorage) cannot substitute for JSON files on disk |

**Conclusion:** A browser-only React SPA cannot implement this application. The C# backend must remain running locally and all dangerous operations must go through it.

---

## 12. Current Tests and What Safety Behavior They Protect

| Test File | Count (approx) | Safety Behavior Protected |
|-----------|----------------|--------------------------|
| `PathSafetyTests.cs` | ~15 | Path traversal prevention; GetSafePath contract |
| `FileInstallerTests.cs` | ~20 | Core install/extract behavior |
| `FileInstallerSafetyPolicyTests.cs` | ~10 | Overwrite policy decisions |
| `RollbackHardeningTests.cs` | ~15 | Rollback on partial install failure |
| `InstallIntegrationTests.cs` | ~15 | End-to-end install with real temp dirs |
| `InstallUninstallIntegrationTests.cs` | ~10 | Round-trip install + uninstall |
| `FileInstallerArchiveTests.cs` | ~10 | Archive extraction via FakeArchive |
| `BackupServiceTests.cs` | ~12 | Backup creation and restore |
| `RestorePointTests.cs` | ~10 | Restore point snapshot and recovery |
| `TransactionServiceTests.cs` | ~8 | Install transaction tracking |
| `ModLibraryServiceTests.cs` | ~15 | Library persistence and enable/disable |
| `PersistenceAndConflictTests.cs` | ~10 | JSON persistence backward compat |
| `BackwardCompatTests.cs` | ~8 | JSON format backward compatibility |
| `ProfileManagerTests.cs` | ~10 | Profile create/switch/delete |
| `CleanupApplyServiceTests.cs` | ~12 | Cleanup: backup-before-delete invariant |
| `CleanupScannerTests.cs` | ~10 | Classification of LSPDFR candidates |
| `CleanupModeTests.cs` | ~8 | Mode policy behavior |
| `GtaFileBackupServiceTests.cs` | ~8 | Backup ZIP before cleanup |
| `SafeLaunchTests.cs` | ~10 | Emergency recovery / safe mode |
| `ArchitectureGuardTests.cs` | ~5 | Layer boundary enforcement |
| `NavigationSmokeTests.cs` | ~17 | All 17 routes reachable without crash |
| `ButtonWiringSmokeTests.cs` | ~20 | All buttons wired to commands |
| `UiRedesignXamlContractTests.cs` | ~10 | XAML contract compliance |
| `VersionDetectorServiceTests.cs` | ~9 | Executable version + hash detection |
| `SetupWizardTests.cs` | ~4 | First-launch wizard scanning |
| `ServiceIntegrationTests.cs` | varies | Cross-service integration |
| `OpenIvExecutorIntegrationTests.cs` | ~8 | OpenIV install pipeline |
| `OivTests.cs`, `OivGuardrailTests.cs`, `OivCreatorTests.cs` | ~30 | OIV creation and install safety |

**Total: 878 tests passing as of v3.7.17.**

---

## 13. Migration Risks

### High risk

| Risk | Detail |
|------|--------|
| **Install pipeline rewrite** | The FileInstaller + PathSafety + rollback chain has 878 tests. Any gap in the API boundary could break the safety invariant. |
| **File picker in React** | Browser `<input type=file>` gives a File object with no full path on most browsers; a native file dialog must be invoked server-side or via desktop shell IPC. |
| **Long-running operations** | Install, backup, cleanup, diagnostics can take 10–60+ seconds. WPF uses async/await + progress binding. A REST API needs SSE or polling for progress. |
| **Path traversal via API** | If mod source paths come from React, the API must re-validate them server-side with PathSafety. Client-side validation is insufficient. |
| **WebView2 in Browse tab** | Cannot be replicated in a browser-only React app; requires the desktop shell to host it. |
| **AppData persistence migration** | Any schema changes to JSON files must be backward-compatible or ship a migration. |
| **Cancellation** | Install, backup, and diagnostics operations need `CancellationToken` plumbing through the API layer. |

### Medium risk

| Risk | Detail |
|------|--------|
| **Architecture choice** | Wrong shell choice (Electron vs WebView2 host vs raw HTTP) affects DX, release packaging, and auto-update. |
| **CORS / localhost security** | A local API running on `localhost:PORT` must not be accessible from the public internet. |
| **State synchronization** | WPF has real-time property binding. React needs polling, WebSocket, or SSE to stay in sync with background operations. |
| **Test coverage gap** | API layer and React components will start with zero test coverage. |

### Low risk

| Risk | Detail |
|------|--------|
| **Domain model serialization** | C# records serialize cleanly to JSON; TypeScript DTOs straightforward to derive. |
| **Browse tab** | Already uses a web-based scraper; easiest tab to migrate conceptually. |
| **Read-only screens** | Dashboard, History, LogViewer, Diagnostics report are read-only and low-risk to migrate first. |

---

## 14. Open Questions / Assumptions

1. **Desktop shell**: Is Electron acceptable, or is a WebView2-hosted WPF shell (keeping `.exe` delivery) strongly preferred? This is the biggest architecture decision.
2. **Packaging**: The current release is a single `.exe` + DLLs ZIP. Will the React migration change the packaging format? Can Electron add size?
3. **Minimum Windows version**: Currently .NET 8 + Windows; does the migration need to preserve the same minimum version?
4. **WebView2 Browse tab**: Must this remain a full embedded browser (WebView2), or can it be simplified to a search-results list backed by the existing scraper API?
5. **Auto-update**: The current app has an `UpdateWorkflowController`. Where does update delivery live in the new architecture?
6. **Parallel operation**: Does the user expect to keep using the WPF app during the migration, or is a full cutover acceptable once React reaches parity?
7. **Feature flags**: `FeatureFlagService` controls some UI. Should this be preserved as-is in the API, or replaced?
8. **`ArchitectureGuardTests`**: These enforce layer boundaries in C#. Equivalent enforcement will be needed for the new API + React layer.
