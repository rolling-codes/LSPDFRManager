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
| **Backup & Restore** | Create full backups of the mod library and configuration snapshots. |
| **Export/Import** | Share a mod list with others or migrate to a new installation using `.lspmanifest` files. |
| **Release Automation** | GitHub Actions validates builds and tests before publishing tagged Windows release artifacts. |

### Supported GTA V Mod Types

- **LSPDFR Plugins** — Automated installation to `plugins/lspdfr/`.
- **Vehicle Add-Ons & Replacements** — Full support for DLC RPFs and YTD/YFT files.
- **ASI Mods & Scripts** — Management of `.asi`, `.cs`, `.vb`, and `.lua` scripts.
- **Custom Content** — Support for EUP Clothing, Map/MLO interiors, and Sound Packs.

---

## Current Release: v3.2.1

LSPDFR Manager v3.2.1 is a bugfix release focused on safer installs, restored desktop build files, fixed WPF navigation, and corrected release packaging links.

- **Safer** — Path traversal and rollback behavior hardened for archive and OpenIV installs.
- **Usable** — Navigation, missing styles, Browse detail installs, and install error visibility restored.
- **Releasable** — Solution/project files and download links now match the release workflow.

See the [v3.2.1 Release Notes](RELEASE_v3.2.1.md) for details.

**Download:** [LSPDFRManager-v3.2.1-win-x64.zip](https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.2.1/LSPDFRManager-v3.2.1-win-x64.zip)

The ZIP extracts to a single `LSPDFRManager-v3.2.1` folder. Keep `LSPDFRManager.exe`, the DLLs, and `run.bat` together in that folder.

---

## Getting Started

### Quick Install (PowerShell)

Use the following command to download and install the latest version:

```powershell
powershell -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/setup.ps1'))"
```

### Requirements

- **OS:** Windows 10 / 11 (x64)
- **Runtime:** .NET 8 Desktop Runtime (automatically installed if missing)
- **Game:** Grand Theft Auto V

---

## Usage

1. **Setup:** Launch the app and set the GTA V installation path in **Settings**.
2. **Install:** Drag any mod archive into the **Install** tab.
3. **Manage:** Use the **Library** tab to toggle mods or uninstall them.
4. **Backup:** Periodically create backups in **Settings**.

### Browse API

The **Browse** tab uses the separate local API service at `http://localhost:5284`. Start `LSPDFRManager.Api` alongside the desktop app when using catalog search and direct downloads.

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln
```

### Repository References

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Release notes: [RELEASE_v3.2.1.md](RELEASE_v3.2.1.md)
- Desktop app project: [LSPDFRManager.csproj](LSPDFRManager.csproj)
- Solution: [LSPDFRManager.sln](LSPDFRManager.sln)
- Domain models: [Domain/](Domain/)

---

## Contributing & Support

Contributions are welcome. See the [Contributing Guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).

**LSPDFR Manager** is licensed under the [MIT License](LICENSE).

---
*Disclaimer: This project is not affiliated with Rockstar Games or the LCPDFR team.*
