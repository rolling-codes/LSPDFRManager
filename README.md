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

## Current Release: v1.5.0

LSPDFR Manager is built on a more stable foundation with improved startup behavior, safer UI-thread handling, and cleaner release automation.

- **Stable** — Core startup and threading issues resolved
- **Reliable** — Reduced risk of UI crashes caused by background operations
- **Maintainable** — Cleaner structure for long-term open-source development

See the [v1.5.0 Release Notes](RELEASE_v1.5.0.md) for details.

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

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet build
```

---

## Contributing & Support

Contributions are welcome. See the [Contributing Guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).

**LSPDFR Manager** is licensed under the [MIT License](LICENSE).

---
*Disclaimer: This project is not affiliated with Rockstar Games or the LCPDFR team.*
