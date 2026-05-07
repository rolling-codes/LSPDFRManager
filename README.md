# LSPDFR Manager — The Ultimate GTA V Command Center

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

**LSPDFR Manager** is an open-source desktop **command center for Grand Theft Auto V (GTA V)** and the **LSPD First Response (LSPDFR)** plugin. Built with .NET 8 and WPF, it provides a complete workflow for installing, tracking, diagnosing, and managing your entire GTA V mod library.

Designed for **LSPDFR plugins**, **vehicle add-ons**, and **GTA V script installations**, LSPDFR Manager focuses on stable mod management, clear organization, deep diagnostics, and practical recovery tools.

---

## Current Release: v3.5.0 — Command Center Update

v3.5.0 is a major release that expands LSPDFR Manager from a mod organizer into a full GTA V/LSPDFR command center.

**Download:** [LSPDFRManager-v3.5.0-win-x64.zip](https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.5.0/LSPDFRManager-v3.5.0-win-x64.zip)

See the [v3.5.0 Release Notes](RELEASE_v3.5.0.md) for full details.

---

## Navigation

| Tab | Description |
|:----|:------------|
| **Home** | Dashboard with status overview and quick actions |
| **Library** | Browse and manage your installed mod collection |
| **Install** | Drag-and-drop mod installation with parallel detection |
| **Browse** | Embedded Chromium browser for lcpdfr.com |
| **Diagnostics** | Plugin health, dependency check, conflict detection, storage |
| **Profiles** | Launch profiles — switch mod setups safely |
| **Backups** | Create backups and manage restore points |
| **History** | Full log of every change LSPDFR Manager has made |
| **Logs** | In-app viewer for manager and GTA V log files |
| **Settings** | All configuration, validation, and update check |

---

## Key Features

| Feature | Description |
|:---|:---|
| **Dashboard** | One-screen overview of GTA V path, LSPDFR status, mod counts, storage, and last backup. |
| **Library Management** | Browse installed mods, search by name/author/type, filter by category. |
| **One-Click Toggle** | Bulk enable or disable mods. Uses file renaming — no data loss. |
| **Smart Installation** | Drag-and-drop archives. Auto-detects mod type with confidence scoring. |
| **Parallel Batch Detection** | Drop multiple archives — all detected in parallel and queued automatically. |
| **Embedded Browser** | Full Chromium browser pointed at lcpdfr.com. Login persists. Click "Install This Mod" to queue automatically. |
| **Plugin Health Scanner** | Scans for duplicates, zero-byte files, bad extractions, mod archives left in game folder. |
| **Dependency Manager** | Checks 16 known GTA V/LSPDFR dependencies. Shows Installed / Missing / Disabled status. |
| **Crash Log Analyzer** | Reads RPH, ScriptHookV, and SHVDN logs — explains likely crash causes in plain language. |
| **Launch Profiles** | Switch between mod setups (Vanilla, LSPDFR Only, Heavy Modded, etc.) safely with one click. |
| **Safe Launch Mode** | Temporarily disable optional plugins for troubleshooting. Creates a restore point first. |
| **Mod Conflict Detector** | Detects multiple gameconfig.xml files, multiple dispatch mods, duplicate SHVDN installs, and more. |
| **Restore Points** | Auto-created before profile switches, safe launch, and uninstalls. One-click restore. |
| **Backup Scheduler** | Schedule backups: manual, every launch, daily, weekly. Enforces max backup count. |
| **Change History** | Tracks every install, enable/disable, profile switch, backup, and recovery operation. |
| **Setup Wizard** | First-run wizard with auto-detection of Steam/Epic/Rockstar installs. |
| **Smart Installer v2** | Preview install plan before writing files. Shows overwrite risks and README instructions. |
| **Mod Metadata Editor** | Add custom names, tags, source URLs, and notes to any installed mod. |
| **Loadout Import/Export** | Share your mod list via `.lspmanifest` files. |
| **GTA V Path Auto-Detection** | Finds your GTA V folder from Steam, Epic, Rockstar, and common paths. |
| **Game Version Checker** | Detects GTA5.exe version and warns if the game updated since last scan. |
| **Release Update Checker** | Check for new LSPDFR Manager releases directly from Settings. |
| **Log Viewer** | Read and search RagePluginHook.log, ScriptHookV.log, and manager logs in-app. |
| **Storage Usage Analyzer** | Shows how much space plugins, mods, backups, and logs use. |
| **Disabled Mods Manager** | List and re-enable all files disabled via the .disabled rename method. |
| **Pre-Launch Checklist** | Quick go/no-go check before launching GTA V or RPH. |
| **Emergency Recovery Mode** | Disable all optional plugins, non-essential ASI, or scripts — always creates a restore point first. |
| **Settings Validation** | Detects invalid paths, unwritable folders, and misconfigured Browse API settings. |
| **220 tests, 0 failures.** | Full xUnit suite covering all core services and all new Command Center features. |

### Supported GTA V Mod Types

- **LSPDFR Plugins** — Automated installation to `plugins/lspdfr/`.
- **Vehicle Add-Ons & Replacements** — Full support for DLC RPFs and YTD/YFT files.
- **ASI Mods & Scripts** — Management of `.asi`, `.cs`, `.vb`, and `.lua` scripts.
- **Custom Content** — Support for EUP Clothing, Map/MLO interiors, and Sound Packs.

---

## Requirements

- **OS:** Windows 10 / 11 (x64)
- **Runtime:** .NET 8 Desktop Runtime (automatically installed if missing via `run.bat`)
- **Browser:** WebView2 Runtime (pre-installed on Windows 11; download from [Microsoft](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) if needed)
- **Game:** Grand Theft Auto V

---

## Getting Started

### Quick Install (PowerShell)

```powershell
powershell -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/setup.ps1'))"
```

### Manual Install

1. Download `LSPDFRManager-v3.5.0-win-x64.zip` from the [Releases](https://github.com/rolling-codes/LSPDFRManager/releases) page.
2. Extract to any folder.
3. Run `LSPDFRManager.exe` (or `run.bat` which also starts the API service).

---

## Usage

1. **First Launch:** The Setup Wizard opens automatically. Select your GTA V folder and backup location.
2. **Home:** Check the Dashboard — status cards show everything at a glance.
3. **Install:** Drag any mod archive into the **Install** tab, or use **Browse** to download from lcpdfr.com.
4. **Diagnose:** Run a full scan in the **Diagnostics** tab before launching.
5. **Launch:** Click **Launch RPH** from the Dashboard quick actions.
6. **Recover:** If something breaks, use **Profiles → Apply** or **Backups → Restore Point**.

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln
dotnet test
```

---

## Contributing & Support

Contributions are welcome. See the [Contributing Guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).

**LSPDFR Manager** is licensed under the [MIT License](LICENSE).

---

*Disclaimer: This project is not affiliated with Rockstar Games or the LCPDFR team.*
