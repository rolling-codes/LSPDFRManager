# LSPDFR Manager v3.5.0 — Command Center Update

**Release Date:** 2026-05-06
**Version:** 3.5.0

---

## Overview

v3.5.0 is a major feature release that transforms LSPDFR Manager from a mod organizer into a full GTA V/LSPDFR command center. It adds diagnostics, crash analysis, launch profiles, safe launch mode, restore points, backup scheduling, dependency scanning, plugin health scanning, conflict detection, change history, smarter installs, setup assistance, and better recovery tools — alongside a complete navigation overhaul.

---

## New Navigation

The sidebar now has 10 tabs:

| Tab | Purpose |
|:----|:--------|
| **Home** | Dashboard overview — status cards, quick actions |
| **Library** | Browse and manage installed mods |
| **Install** | Drag-and-drop install with parallel detection |
| **Browse** | Embedded browser for lcpdfr.com |
| **Diagnostics** | Plugin health, dependencies, conflicts, storage |
| **Profiles** | Launch profiles and profile switching |
| **Backups** | Backups and restore points |
| **History** | Full change history log |
| **Logs** | In-app log viewer |
| **Settings** | All configuration |

---

## New Features (25)

### 1 — Dashboard Home Screen
- Status cards: GTA V path, LSPDFR, mods enabled/disabled, storage used, last backup, last diagnostics scan, last crash log
- Quick actions: Launch RPH, Launch GTA V, Scan Plugins, Analyze Crash Logs, Create Backup, Safe Launch Mode, Open GTA V Folder, Open Logs Folder

### 2 — Browse API Service Manager
- Detects whether LSPDFRManager.Api is running
- Start, Stop, Restart buttons in Browse tab
- Searches beside the exe, in `./Api/`, and `./LSPDFRManager.Api/`
- Status: Offline / Starting / Online / Error / MissingExecutable / PortConflict
- Logs to `logs/browse_api_service.log`

### 3 — Full Diagnostics Center
- Left-side category selector (Plugin Health, Dependencies, Conflicts, Storage)
- Detail panel for each finding with recommended fix
- Severity filters: Ok / Info / Warning / Error / Critical
- Export to JSON, TXT, or HTML

### 4 — Plugin Health Scanner
- Scans plugins/, lspdfr/, scripts/, mods/, ELS/, pack_default/
- Detects: duplicates, zero-byte DLLs, mod archives left in game folder, disabled files, copy/backup naming, misplaced READMEs

### 5 — Dependency Manager
- Checks 16 known dependencies (GTA5.exe, RPH, LSPDFR, ScriptHookV, NativeUI, etc.)
- Status: Installed / Missing / Disabled / Optional
- Mark as ignored

### 6 — Crash Log Analyzer
- Reads RagePluginHook.log, ScriptHookV.log, ScriptHookVDotNet.log, manager log
- 19 keyword patterns → suspected cause, recommended fix, severity
- Export crash report to JSON, TXT, or HTML

### 7 — Launch Profiles
- 7 default profiles: Vanilla GTA V, LSPDFR Only, Stable Patrol, Heavy Modded Patrol, Testing New Plugins, Minimal Safe Mode, Recording/Streaming Mode
- Create, duplicate, rename, delete, import, export
- Apply creates a restore point automatically before switching

### 8 — Safe Launch Mode
- 5 modes: LSPDFR Only, Vanilla GTA V, Disable Recent Mods, Disable Non-Essential ASI, Disable Scripts
- Shows plan before applying, creates restore point, uses .disabled renaming (never deletes files)

### 9 — Mod Conflict Detector
- 10 conflict groups: Dispatch/Police AI, Traffic Density, Handling, Gameconfig, Visual Settings, Callouts, EUP, Sound/Sirens, Heap/Packfile, ScriptHookV DotNet
- Detects multiple gameconfig.xml files
- Shows severity, reason, and suggested fix

### 10 — Change History
- Tracks 17 action types: install, uninstall, enable/disable, profile apply, safe launch, backup, restore, scan, crash analysis, API events, and more
- Filter by action type or search by description/file
- Export to JSON or TXT, clear with confirmation

### 11 — Restore Points
- Auto-created before: profile switch, safe launch, uninstall, emergency recovery
- Stores enabled/disabled state per file, with timestamp and operation name
- Restore or delete from Backups tab
- Index capped at 50 most recent

### 12 — Backup Scheduler
- Schedule modes: Manual, Every Launch, Daily, Weekly, Before Profile Switch, Before Install, Before Safe Launch
- Enforces `MaxBackupCount` (default 10) — oldest deleted automatically
- Backup manifest tracked in `data/backup_manifest.json`

### 13 — First-Time Setup Wizard
- 7-page wizard: Welcome → Detect GTA V → Validate → Dependencies → Backup Folder → Browse API → Finish
- Auto-detects from Steam, Epic, Rockstar Launcher, registry, common paths on C:/D:/E:
- Validates path and warns about OneDrive / Program Files locations

### 14 — Smart Mod Installer v2
- Preview install plan before writing any files
- Shows per-file risk: Safe / Overwrite / Suspicious / Incompatible
- Warns about overwritten files, embedded executables, path traversal
- Shows README/install instructions from archive
- Dry-run mode

### 15 — Mod Metadata Editor
- Editable: custom name, author, version, source URL, notes, tags, favorite, risky, ignored-by-diagnostics
- Saved to `data/mod_metadata.json`

### 16 — Mod Loadout Import/Export
- Export enabled + disabled mod list to `.lspmanifest`
- Import and compare with current setup (missing/extra mods)
- Includes game version and manager version

### 17 — GTA V Path Auto-Detection
- Reads Steam, Epic, Rockstar registry entries
- Checks common paths on C:, D:, E:
- Validates each candidate for GTA5.exe presence

### 18 — Game Version Checker
- Reads GTA5.exe file version
- Detects if version changed since last check
- Shown on Dashboard

### 19 — Release Update Checker
- Checks GitHub releases API for a newer version
- Manual check from Settings; optional auto-check on startup
- Gracefully handles offline / no internet

### 20 — Log Viewer
- In-app viewer for: manager log, Browse API log, RagePluginHook.log, ScriptHookV.log, ScriptHookVDotNet.log
- Search and severity filter
- Export selected lines, clear manager log, open containing folder

### 21 — Storage Usage Analyzer
- Reports folder size + file count for: plugins, lspdfr, scripts, mods, ELS, backups, restore points, profiles, logs, app data
- Shown as MB/KB

### 22 — Disabled Mods Manager
- Lists all `.disabled` files across plugins, lspdfr, scripts, mods, ELS
- Shows original name, category, likely mod name
- Enable individual files

### 23 — Pre-Launch Checklist
- Checks: GTA V path, GTA5.exe, RAGEPluginHook.exe, LSPDFR.dll, ScriptHookV.dll, disk space, recent crash warning
- Result: Ready / Ready with warnings / Not ready (blocking failures)

### 24 — Emergency Recovery Mode
- Modes: Disable All Optional Plugins, Disable Non-Essential ASI, Disable Scripts Folder
- Creates restore point before applying
- Uses .disabled renaming only — never deletes files

### 25 — Settings Validation Center
- Validates: GTA V path, backup path writability, app data writability, Browse API path, Browse API URL format
- Reports blocking vs non-blocking issues

---

## AppConfig New Fields

```json
"AutoStartBrowseApi": false,
"BrowseApiPath": null,
"BrowseApiBaseUrl": "http://localhost:5284",
"AutoBackupEnabled": false,
"BackupScheduleMode": 0,
"MaxBackupCount": 10,
"CompressBackups": true,
"ActiveProfileId": null,
"ShowSetupWizardOnStartup": true,
"LastDiagnosticsScanUtc": null,
"CheckForUpdatesOnStartup": false,
"LastUpdateCheckUtc": null,
"LastKnownGameVersion": null,
"LastKnownGameVersionDate": null
```

All fields have safe defaults — existing `config.json` files load correctly.

---

## New App Data Paths

All written to `%APPDATA%\LSPDFRManager\`:

| Path | Contents |
|:-----|:---------|
| `profiles/*.json` | Launch profiles |
| `restore_points/index.json` | Restore point index |
| `data/change_history.json` | Change history |
| `data/mod_metadata.json` | Mod metadata |
| `data/backup_manifest.json` | Backup manifest |
| `logs/browse_api_service.log` | Browse API log |

---

## Tests

- 51 new tests in `CommandCenterTests.cs` covering all 25 new features
- **Full suite: 220 tests, 0 failures**

---

## Validation

```bash
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --configuration Release
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.5.0 -p:DebugType=None -p:DebugSymbols=false
```

---

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Release download: https://github.com/rolling-codes/LSPDFRManager/releases/tag/v3.5.0
