# LSPDFR Manager - The Ultimate GTA V Mod Organizer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

**LSPDFR Manager** is an open-source desktop **mod manager for Grand Theft Auto V (GTA V)** and the **LSPD First Response (LSPDFR)** plugin. Built with .NET 8 and WPF, it provides a streamlined workflow for installing, tracking, and managing a GTA V mod library.

Designed for **LSPDFR plugins**, **vehicle add-ons**, and **GTA V script installations**, LSPDFR Manager focuses on stable mod management, clear organization, and practical release automation.

---

## Key Features

| Feature | Description |
|:---|:---|
| **Library Management** | Browse installed mods, search by name/author/type, and filter by category. |
| **One-Click Toggle** | Bulk enable or disable mods instantly. Uses file renaming to safely toggle mods without data loss. |
| **Smart Installation** | Drag-and-drop mod archives (`.zip`, `.rar`, `.7z`). Auto-detects mod type with high-confidence scoring. |
| **Parallel Batch Detection** | Drop multiple archives at once — all detected in parallel and queued automatically. |
| **Embedded Browser** | Full Chromium browser in the Browse tab pointed at lcpdfr.com. Browse, log in, and install mods directly. Login persists across sessions. |
| **Auto-Install** | Optionally skip the confirmation step for high-confidence detections (≥ 75%). |
| **Backup & Restore** | Create full backups of the mod library and configuration snapshots. |
| **Export/Import** | Share a mod list with others or migrate to a new installation using `.lspmanifest` files. |
| **Release Automation** | GitHub Actions validates builds and tests before publishing tagged Windows release artifacts. |

### Supported GTA V Mod Types

- **LSPDFR Plugins** — Automated installation to `plugins/lspdfr/`.
- **Vehicle Add-Ons & Replacements** — Full support for DLC RPFs and YTD/YFT files.
- **ASI Mods & Scripts** — Management of `.asi`, `.cs`, `.vb`, and `.lua` scripts.
- **Custom Content** — Support for EUP Clothing, Map/MLO interiors, and Sound Packs.

---

## Current Release: v3.3.0

v3.3.0 ships a full embedded browser in the Browse tab and three install speed-up features.

- **Browse** — WebView2 Chromium browser pointed at lcpdfr.com. Log in once, browse the full site, click "Install This Mod" — the archive is intercepted, detected, and queued automatically.
- **Faster installs** — Parallel batch detection, auto-install for high-confidence mods, and auto-cleanup of temp download files.
- **169 tests, 0 failures.**

See the [v3.3.0 Release Notes](RELEASE_v3.3.0.md) for full details.

**Download:** [LSPDFRManager-v3.3.0-win-x64.zip](https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.3.0/LSPDFRManager-v3.3.0-win-x64.zip)

The ZIP extracts to a single `LSPDFRManager-v3.3.0` folder. Keep `LSPDFRManager.exe`, the DLLs, and `run.bat` together in that folder.

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

1. Download `LSPDFRManager-v3.3.0-win-x64.zip` from the [Releases](https://github.com/rolling-codes/LSPDFRManager/releases) page.
2. Extract to any folder.
3. Run `LSPDFRManager.exe` (or `run.bat` which also starts the API service).

---

## Usage

1. **Setup:** Launch the app and set the GTA V installation path in **Settings**.
2. **Install:** Drag any mod archive into the **Install** tab, or use the **Browse** tab to download directly from lcpdfr.com.
3. **Manage:** Use the **Library** tab to toggle mods on/off or uninstall them.
4. **Backup:** Periodically create backups in **Settings**.

### Speed-Up Settings

| Setting | Default | Description |
|:---|:---|:---|
| Auto-install high-confidence mods | Off | Queues mods with ≥ 75% confidence without clicking Install |
| Delete temp files after install | On | Removes browser-downloaded archives from the temp folder |

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
