# LSPDFR Manager

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/badge/release-v3.7.11-blue.svg)](https://github.com/rolling-codes/LSPDFRManager/releases/latest)

A complete GTA V and LSPDFR command center — install and manage mods, run diagnostics, switch profiles, analyze crashes, and launch safely. Built with .NET 8 WPF.

**[Download Latest Release →](https://github.com/rolling-codes/LSPDFRManager/releases/latest)**

---

## Features

| Tab | What it does |
|-----|-------------|
| **Home** | Dashboard with mod count, status indicators, and quick-action buttons |
| **Library** | Browse, search, filter, enable/disable, and uninstall all installed mods |
| **Install** | Drag-and-drop or browse for mod archives — smart plan with conflict detection and rollback |
| **Browse** | Embedded lcpdfr.com browser with one-click install queuing |
| **Diagnostics** | Plugin health scanner, dependency checker, crash log analyzer |
| **Profiles** | Launch profiles — switch full mod loadouts with a single click |
| **Backups** | Scheduled and on-demand backups of library and config state |
| **History** | Full change log of every install, uninstall, enable, and disable action |
| **Logs** | Real-time LSPDFR/GTA V log viewer |
| **OIV Tools** | Build OIV packages with a creator wizard, or install existing `.oiv` files |
| **Mod Config** | Per-plugin config file editor with raw and parsed views |
| **Settings** | GTA V path, backup location, behavior toggles, update checker |

### Detection Engine

Automatically identifies mod type from archive contents:

| Mod Type | Detection signal |
|----------|-----------------|
| LSPDFR Plugin | `plugins/lspdfr/*.dll` |
| ASI Mod | `*.asi` at root |
| Vehicle Add-On DLC | `dlcpacks/*/dlc.rpf` |
| Vehicle Replace | `x64/levels/gta5/vehicles.rpf` |
| Script | `scripts/*.cs` or `scripts/*.dll` |
| EUP | `x64e.rpf` or `eup/` paths |
| Map | `*.ymap` or `maps/` |
| Sound | `x64/audio/**/*.awc` |

Each detection produces a confidence score (Low / Medium / High). Low-confidence results warn before installing.

### Smart Install Planner

Before any file is written, the planner builds a full install plan:

- Detects conflicts against enabled mods, disabled mods, and `.disabled` files on disk
- Surfaces overwrite risks and blocking issues in the review UI
- Orders files by dependency priority (RPH, ELS, shared DLLs)
- The reviewed plan is what actually executes — no double-build at install time

### Safety

- **Atomic installs** — any failure triggers a full rollback; no partial installs reach disk
- **Path traversal protection** — archive entries validated through `PathSafety` before extraction
- **Enable/Disable** — renames files with `.disabled` suffix; GTA V ignores them without deletion
- **Conflict detection** — warns when incoming mod files overlap with installed mods (including disabled ones)
- **OIV path validation** — install paths in OIV packages are checked for traversal and rooted-path injection before packaging or install

---

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) — required for Browse tab (pre-installed on Windows 11)
- Grand Theft Auto V

---

## Install

Download `LSPDFRManager-v3.7.11-win-x64.zip` from [Releases](https://github.com/rolling-codes/LSPDFRManager/releases/latest), extract anywhere, and run `LSPDFRManager.exe`. No installer needed.

The first run will prompt you to set your GTA V path if it is not auto-detected.

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
```

**Self-contained release build:**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

---

## Project Structure

```
LSPDFRManager/
├── Domain/          # Data models (InstalledMod, ModInfo, AppConfig, InstallPlan…)
├── Services/        # Business logic (ModDetector, FileInstaller, SmartInstallPlanner,
│                    #   BackupService, OivService, DiagnosticsOrchestrator…)
├── ViewModels/      # MVVM view models; MainViewModel orchestrates tab navigation
├── Views/           # WPF UserControls, one per tab
│   └── Components/  # Shared sub-controls (ModCard…)
├── Converters/      # WPF value converters
├── Core/            # AppLogger, InstallQueue, UiDispatcher, PathSafety
│   └── Commands/    # IAppCommand, AsyncAppCommand
├── Features/        # Feature slices (Install, Library, OivCreatorTemplates, Updates…)
├── Resources/       # Colors.xaml (design tokens), Styles.xaml (component styles)
└── LSPDFRManager.Tests/  # xUnit test suite (734 tests)
```

Key singletons: `ModLibraryService`, `AppConfig`, `InstallQueue`, `LspdfrStatusService`

All persistent data lives in `%APPDATA%\LSPDFRManager\`:
- `library.json` — installed mod registry
- `config.json` — app settings
- `configs.json` — captured mod config snapshots
- `Backups/` — ZIP backup archives
- `app.log` — runtime log

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Buttons invisible / blank UI | Reinstall [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Browse tab blank | Install/repair [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |
| GTA V not detected | Settings → set path manually |
| SmartScreen warning | "More info" → "Run anyway" |
| File blocked after extract | Right-click → Properties → Unblock |
| Errors after update | Delete `%APPDATA%\LSPDFRManager\library.json` to reset the library |
| Other | Check `%APPDATA%\LSPDFRManager\app.log` |

---

Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Licensed [MIT](LICENSE).

*Not affiliated with Rockstar Games or the LCPDFR/LSPDFR team.*
