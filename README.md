# LSPDFR Manager

A desktop mod manager for GTA V and [LSPDFR](https://www.lcpdfr.com/lspdfr/), built with .NET 8 WPF.

Install, track, enable/disable, back up, and export your entire mod library from one place.

---

## Features

| Tab | What it does |
|-----|-------------|
| **Library** | Browse all installed mods, search by name/author/type, filter by category, sort by install date/name/author/status, bulk enable/disable visible mods, uninstall |
| **Install** | Drag-and-drop or browse for a mod archive (`.zip` / `.rar` / `.7z`); auto-detects mod type with confidence scoring; installs into your GTA V folder |
| **Settings** | Set your GTA V path, configure backup behavior, create/restore backups, export and import mod manifests |

### Supported Mod Types

- **LSPDFR Plugin** — DLLs in `plugins/lspdfr/`
- **Vehicle Add-On DLC** — `dlcpacks/` RPF packages
- **Vehicle Replace** — YTD/YFT replacements
- **ASI Mod** — `.asi` files
- **Script** — `.cs` / `.vb` / `.lua` scripts
- **EUP Clothing** — YDD/YTD ped packs
- **Map / MLO** — YMAP / YTYP / YBN interiors
- **Sound Pack** — AWC / REL audio files

Detection is scored automatically from path patterns, file extensions, and archive name keywords. Low-confidence detections show a warning before install.

---

## Current Release: v1.4.0

**Production-ready** with streaming installer and comprehensive observability.

- **Memory Efficient** — Streaming pipeline reduces peak memory from archive size to ~2MB buffer (200MB archives previously used 200MB+ RAM)
- **Adaptive Buffering** — File size-aware buffer selection (64KB–2MB) for optimal throughput on all systems
- **Fully Observable** — Installation logs include version tags, session IDs, detailed operation tracking (extraction, retry behavior, rollback cleanup), and stack traces for production debugging
- **Battle-Tested** — 149 test validations covering streaming, rollback, error recovery, and edge cases
- **Backward Compatible** — All existing mod installations work unchanged; zero API breaking changes

See [v1.4.0 Release Notes](RELEASE_v1.4.0.md) for details.

---

## Download & Install

### Quick Install (One-Liner)

```powershell
powershell -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/setup.ps1'))"
```

Automatically downloads v1.4.0, extracts to `C:\Program Files\LSPDFRManager\`, and launches.

### Manual Download

Grab the latest release from [Releases](https://github.com/rolling-codes/LSPDFRManager/releases/latest) — extract and run `run.bat`.

---

## Requirements

- Windows 10 / 11 (x64)
- .NET 8 Desktop Runtime (auto-installed by launcher if missing)
- GTA V installed
- LSPDFR installed if you're managing LSPDFR plugins

### Install .NET 8 (If Needed)

```powershell
powershell -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/install-dotnet.ps1'))"
```

Or manually: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

## First-Time Setup

1. Launch `LSPDFRManager.exe`
2. Go to **Settings** and set your **GTA V folder** (e.g. `C:\Program Files\Rockstar Games\Grand Theft Auto V`)
3. Click **Save Settings**

That's it — you're ready to install mods.

---

## Usage

### Installing a mod

1. Go to the **Install** tab
2. Drop a mod archive onto the drop zone, or click **Browse for file**
3. Review the detected mod type and confidence level
4. Optionally enter the author name
5. Click **Install Mod**

The mod is extracted into your GTA V folder and registered in the library.

### Managing the library

- Use the **search bar** to filter by name, author, or type
- Use the **type dropdown** to show only one category
- Click the **toggle** on any mod card to enable or disable it (renames installed files with a `.disabled` suffix so GTA V ignores them)
- Click **✕** to uninstall and remove the mod's files

### Backup and restore

Under **Settings > Backup & Restore**, click **Create Backup** to ZIP your library database, config snapshots, and key files. Restore from any previous backup with **Restore from Backup**.

### Exporting / importing a mod list

Use **Export Manifest** to save a `.lspmanifest` JSON file describing all your installed mods. On a new machine, point the importer at your original mod archives and use **Import & Reinstall** to re-install everything automatically.

---

## Building from Source

```
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet build
```

To produce a self-contained executable:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### Dependencies

| Package | Purpose |
|---------|---------|
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | RAR and 7-Zip archive extraction |

---

## Project Structure

```
LSPDFRManager/
├── Models/          Data models (InstalledMod, ModInfo, ModKey, AppConfig, …)
├── Converters/      WPF value converters
├── Core/            AppLogger, InstallQueue (background install processor)
├── Services/        ModDetector, FileInstaller, ModLibraryService,
│                    ConfigManagerService, BackupService,
│                    ExportService, BatchReinstallService
├── ViewModels/      MVVM view models (one per tab + MainViewModel)
├── Views/           WPF UserControls (LibraryView, InstallView, ConfigView, SettingsView)
└── Resources/       Styles.xaml (dark theme)
```

---

## Data Storage

All app data is stored under `%APPDATA%\LSPDFRManager\`:

| File | Contents |
|------|----------|
| `library.json` | Installed mod registry |
| `configs.json` | Captured mod config snapshots |
| `config.json` | App settings (GTA path, backup path, …) |
| `keys/` | Stored key file copies |
| `Backups/` | ZIP backup archives |
| `app.log` | Runtime log |

---

## License

MIT
