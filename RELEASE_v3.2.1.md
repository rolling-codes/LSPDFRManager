# LSPDFRManager v3.2.1 Release Notes

**Release Date:** 2026-05-06
**Version:** 3.2.1

---

## Overview

v3.2.1 is a cleanup and bugfix release that restores the desktop build surface, hardens installer path safety, fixes key WPF UI contracts, refreshes repository references, and aligns release download links with the GitHub Actions artifact name.

## New Features

- Restored the WPF navigation shell so Library, Install, Config, Browse, and Settings are directly reachable again.
- Added a global install failure banner so users see install errors without digging through logs.
- Added visible archive detection progress in the Install tab while mod analysis is running.
- Added focused AppConfig default coverage for `AutoLaunchAfterInstall`.

## Fixes

- Restored the root WPF app project and solution so `dotnet build LSPDFRManager.sln` and test project references work again.
- Hardened archive and OpenIV install paths so rooted, absolute, and traversal paths are rejected before writing files.
- Improved installer rollback so failed installs remove newly-created directories and restore overwritten files.
- Restored missing WPF resources for icon buttons and toggle switches.
- Fixed Browse detail-pane downloads by passing the selected result to the install command.
- Fixed release packaging so `run.bat` is included with the framework-dependent desktop build.
- Changed the release ZIP to contain a top-level `LSPDFRManager-v3.2.1` folder so the executable, DLLs, and launcher stay together.
- Updated setup, workflow, repository, and file references to use `LSPDFRManager-v3.2.1-win-x64.zip`, `LSPDFRManager.sln`, and `Domain/` model paths.

## Repository Links

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Release download: https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.2.1/LSPDFRManager-v3.2.1-win-x64.zip
- Setup script: https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/setup.ps1

The ZIP extracts to `LSPDFRManager-v3.2.1/`. Keep all files in that folder together; the executable depends on the accompanying DLLs.

## Known Limitation

The Browse tab still uses the separate local API service at `http://localhost:5284`. Start `LSPDFRManager.Api` alongside the desktop app to enable catalog search and direct downloads.

## Validation

Release validation should run:

```bash
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --configuration Release
dotnet publish LSPDFRManager.csproj --configuration Release --runtime win-x64 --self-contained false --output publish/v3.2.1 -p:DebugType=None -p:DebugSymbols=false
```
