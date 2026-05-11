# LSPDFR Manager

Open-source GTA V and LSPDFR command center built with .NET 8 and WPF.

LSPDFR Manager is a desktop tool for managing Grand Theft Auto V/LSPDFR mod setups. It focuses on safe mod installation, library tracking, diagnostics, profiles, backups, crash-log analysis, and recovery workflows.

---

## Core Capabilities

| Area | What it provides |
|:---|:---|
| **Mod Library** | Browse installed mods, search, filter, enable, disable, and track metadata. |
| **Smart Installer** | Drag-and-drop archive detection, install planning, path safety checks, rollback, and conflict warnings. |
| **Diagnostics** | Plugin health checks, dependency checks, conflict detection, storage analysis, and exportable reports. |
| **Crash Analysis** | Reads common GTA V/LSPDFR log files and surfaces likely causes in plain language. |
| **Profiles** | Switch between mod setups safely using launch profiles and restore points. |
| **Backups & Recovery** | Restore points, backup scheduling, safe launch mode, and emergency recovery workflows. |
| **Browse Integration** | Embedded WebView2 browser for LCPDFR browsing and mod download routing. |
| **Settings Validation** | Detects invalid paths, missing dependencies, unwritable folders, and configuration issues. |

---

## Supported Mod Types

- LSPDFR plugins
- Vehicle add-ons and replacements
- ASI mods and scripts
- ScriptHookVDotNet scripts
- EUP clothing packs
- Map/MLO content
- Sound and siren packs
- Custom GTA V content archives

---

## Requirements

- Windows 10 or Windows 11 x64
- .NET 8 Desktop Runtime / SDK
- WebView2 Runtime for the embedded browser
- Grand Theft Auto V
- LSPDFR / RAGE Plugin Hook for LSPDFR-specific workflows

---

## Repository Structure

| Path | Purpose |
|:---|:---|
| `.github/workflows/` | CI and release automation. |
| `Core/` | Core application infrastructure. |
| `Domain/` | Domain models and shared types. |
| `Services/` | Installer, library, diagnostics, profile, backup, and recovery services. |
| `ViewModels/` | MVVM view models for the WPF UI. |
| `Views/` | WPF XAML views and UI components. |
| `Converters/` | WPF binding converters. |
| `Resources/` | Styles, templates, icons, and UI resources. |
| `LSPDFRManager.Api/` | Companion API service used by browse/search workflows. |
| `LSPDFRManager.Tests/` | xUnit test coverage for services and workflows. |
| `docs/` | Long-form documentation. |

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --configuration Release
```

To publish a Windows x64 build:

```bash
dotnet publish LSPDFRManager.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained false \
  --output publish/win-x64
```

---

## Development Notes

- Keep installer logic rollback-safe and path-safe.
- Avoid direct file writes without backup or atomic replacement where data loss is possible.
- Keep UI changes accessible: readable contrast, clear states, keyboard-friendly controls, and meaningful labels.
- Add tests for installer, rollback, XML/config patching, profile switching, and diagnostics behavior when changing those systems.

---

## Releases

Release builds and version-specific notes belong on the GitHub Releases page, not as permanent root-level release-note files in the source tree.

---

## License

MIT License. See [`LICENSE`](LICENSE).

---

This project is not affiliated with Rockstar Games, Take-Two Interactive, LCPDFR, LSPDFR, or RAGE Plugin Hook.
