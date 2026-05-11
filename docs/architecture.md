# Architecture

**MVVM WPF Desktop App** (.NET 8, Windows x64 only)

## Layer Structure

- **Views/** — WPF UserControls, one per tab: Dashboard, Library, Install, Browse, Diagnostics, Profiles, Backups, History, Logs, Settings, Config, SetupWizard; plus `Views/Components/` for shared controls
- **ViewModels/** — MVVM view models; MainViewModel orchestrates tab navigation; each tab has a dedicated VM
- **Domain/** — Data classes (InstalledMod, ModInfo, ModType, AppConfig, ModManifest, InstallResult, RestorePoint, ModProfile, etc.)
- **Services/** — Business logic (~42 services: ModLibraryService, ModDetector, FileInstaller, BackupService, ProfileManager, CrashLogAnalyzer, DependencyScanner, DiagnosticsOrchestrator, etc.)
- **Core/** — AppLogger (file logging), InstallQueue (background async install processor), UiDispatcher, `Core/CarInstall/` subfolder
- **Converters/** — WPF value converters (InverseBoolConverter, StringToBrushConverter, StringToVisibilityConverter)
- **LSPDFRManager.Api/** — Separate API service sub-project (Models/, Services/)

## Key Singletons

- **ModLibraryService** — In-memory registry of all installed mods, synced to `library.json`. Search, enable/disable, conflict detection.
- **AppConfig** — App settings (GTA path, backup folder, etc.), synced to `config.json`.
- **LspdfrStatusService** — Monitors live status of LSPDFR/GTA V processes (shown in sidebar).
- **InstallQueue** — Background task processor; installs one mod at a time to avoid file conflicts.

Singletons use lazy-init. Modifications (Add, Remove, SetEnabled) trigger immediate Save to JSON. No locks — assume single-threaded UI access.

## Core Workflows

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
- May fail silently if GTA V holds the file handle; check app.log for rename errors

**Detection Scoring**
- ModDetector analyzes path patterns, file extensions, archive names
- Scores each ModType (LSPDFR Plugin, DLC, Replace, ASI, Script, EUP, Map, Sound) with confidence 0–100
- Low confidence (< 50) warns user; user can override
- Keywords in archive name can boost/flip detection (e.g., "dlcpack" → DLC)
- Path patterns checked recursively (e.g., `plugins/lspdfr/` at any depth)

**Backup/Restore**
- BackupService ZIPs library.json, configs.json, key file copies
- RestoreService extracts snapshot, restores app state

## Storage

All data → `%APPDATA%\LSPDFRManager\`:
- `library.json` — Installed mods registry
- `configs.json` — Captured mod config snapshots
- `config.json` — App settings
- `keys/` — Cached key files
- `Backups/` — ZIP archives
- `app.log` — Runtime log

## MVVM Patterns

- [ObservableObject.cs](../ViewModels/ObservableObject.cs) — base class for VMs, implements INotifyPropertyChanged
- [RelayCommand.cs](../ViewModels/RelayCommand.cs) — simple ICommand for button actions
- [Resources/Styles.xaml](../Resources/Styles.xaml) — centralized dark theme (brushes, templates)
- [GlobalUsings.cs](../GlobalUsings.cs) — cross-cutting imports (System.IO, System.IO.Compression, System.Collections.ObjectModel, System.Text.Json)
- File-scoped namespaces: `namespace LSPDFRManager.ViewModels;` (no braces)
- Nullable reference types enabled: `string?` for nullable, `string` for non-null

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| SharpCompress | 0.38.0 | RAR, 7-Zip archive extraction (ZIP built-in) |
| Microsoft.Web.WebView2 | 1.0.2739.15 | Embedded browser (lcpdfr.com BrowseView) |
| xUnit | 2.9.0 | Unit testing (test project only) |
