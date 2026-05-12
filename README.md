# LSPDFR Manager

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

Open-source GTA V & LSPDFR command center — mod install, diagnostics, profiles, crash analysis, safe launch, and more. Built with .NET 8 WPF.

**[Latest Release →](https://github.com/rolling-codes/LSPDFRManager/releases/latest)**

---

## Features

- Drag-and-drop mod install with auto-detection (LSPDFR plugins, vehicles, ASI, scripts, EUP)
- Embedded lcpdfr.com browser — click "Install This Mod" to queue automatically
- Plugin health scanner, dependency checker, crash log analyzer
- Launch profiles — switch mod setups safely with one click
- Safe Launch Mode and Emergency Recovery with auto restore points
- Mod conflict detection, change history, backup scheduler
- Setup wizard with Steam/Epic/Rockstar auto-detection
- 223 xUnit tests

---

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — `run.bat` installs it automatically
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) — pre-installed on Windows 11
- Grand Theft Auto V

---

## Install

**Download** the ZIP from [Releases](https://github.com/rolling-codes/LSPDFRManager/releases), extract, run `run.bat`.  
See `INSTALL.txt` in the ZIP for full first-run instructions.

**Or via PowerShell:**
```powershell
powershell -ExecutionPolicy Bypass -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/setup.ps1'))"
```

---

## Build from Source

```bash
git clone https://github.com/rolling-codes/LSPDFRManager.git
cd LSPDFRManager
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln
dotnet test
```

See [SOURCE_OVERVIEW.md](SOURCE_OVERVIEW.md) for codebase structure.

---

## Troubleshooting

| Problem | Fix |
|:--------|:----|
| Buttons invisible / blank UI | Reinstall .NET 8 Desktop Runtime |
| Browse tab blank | Install/repair [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |
| App won't start | Install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| GTA V not detected | Settings → set path manually |
| SmartScreen warning | "More info" → "Run anyway" |
| Blocked file after extract | Right-click → Properties → Unblock |
| Other | `%APPDATA%\LSPDFRManager\logs\` |

---

Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Licensed [MIT](LICENSE).

*Not affiliated with Rockstar Games or the LCPDFR team.*
