# LSPDFRManager v3.3.0 Release Notes

**Release Date:** 2026-05-06
**Version:** 3.3.0

---

## Overview

v3.3.0 replaces the old Browse tab search panel with a full embedded Chromium browser (WebView2), adds a formal download bridge between the browser and the install queue, and ships three new speed-up features that reduce the friction of getting mods installed.

---

## New Features

### Embedded Browser (Browse Tab)

- The Browse tab now shows a full WebView2 Chromium browser pointed at `https://www.lcpdfr.com/files/`.
- Users can log in, navigate, and install mods directly from the site — no separate API service required for basic browsing.
- WebView2 session data (cookies, login state) persists across app restarts via a profile stored in `%APPDATA%\LSPDFRManager\WebView2`.
- Browser toolbar includes Back, Forward, Refresh, and an address bar with keyboard navigation (`Enter` to go).
- "Install This Mod" button injects JavaScript to click the site's primary download button, triggering the native download pipeline.
- Download progress is shown in the Browse status bar (KB received / KB total).
- The browser panel fills the full available height so the download button on mod pages is always visible.

### Download Bridge (`ModDownloadBridge`)

- Downloads intercepted from the browser are automatically routed through `ModDetector` → `InstallQueue` without any manual steps.
- Three typed events (`Detecting`, `Queued`, `Failed`) update both the Browse status bar and the Install tab log simultaneously.
- The Install tab shows `[Browse]` prefixed entries for all browser-originated installs, keeping a single unified log.

### Parallel Batch Detection

- Dropping multiple mod archives onto the Install tab now detects all of them in parallel using `Task.WhenAll`.
- Each detected mod is queued immediately after detection completes — no waiting for the full batch.
- Single-file drops continue to behave exactly as before.

### Auto-Install High-Confidence Mods *(opt-in, Settings)*

- When enabled, mods with a detection confidence score of 75% or above are queued for install automatically after detection — no Install button click required.
- Applies to both drag-and-drop installs and browser downloads.
- Disabled by default; toggle in **Settings → App Behaviour**.

### Auto-Delete Temp Files After Install *(opt-in, Settings)*

- After a successful install, the source archive is automatically removed from `%TEMP%\LSPDFRManager_downloads`.
- Only archives in the managed temp folder are affected — user-selected files from their own directories are never deleted.
- Enabled by default; toggle in **Settings → App Behaviour**.

---

## Fixes

- Fixed `BrowseViewModel.NavigateCommand` — address bar navigation now correctly calls `View.NavigateTo()` after the View reference is properly wired.
- Fixed download interception: `e.Handled = true` suppresses the browser Save dialog; downloads go to a controlled temp path.
- Fixed `TotalBytesToReceive` type mismatch (`ulong?` vs `long`) in download progress calculation.
- Fixed browser panel aspect ratio — the WebView2 now fills all remaining vertical space in the Browse layout.
- Fixed `PlaceholderText` / `ConsoleText` resource key collision between brush and style in the dark theme.
- Fixed white ListBox background — implicit `ListBox` and `ListBoxItem` styles now apply the dark theme correctly.

---

## Tests

- 10 new `ModDownloadBridgeTests` covering: mod type detection (LSPDFR Plugin, Vehicle DLC, ASI), event ordering (Detecting before Queued), empty/whitespace path guards, multiple sequential downloads, low-confidence pass-through, and source path preservation.
- Full suite: **169 tests, 0 failures**.

---

## Repository Links

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Release download: https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.3.0/LSPDFRManager-v3.3.0-win-x64.zip

---

## Upgrade Notes

- **WebView2 runtime required.** Most Windows 11 machines already have it. If the Browse tab fails to initialize, download the [WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) from Microsoft.
- The old `LSPDFRManager.Api` service is no longer required for browsing. It can still be started via `run.bat` if you use the API endpoints directly.
- `config.json` gains two new boolean fields: `AutoInstallHighConfidence` (default `false`) and `DeleteTempAfterInstall` (default `true`). Existing config files are read correctly — missing fields use the defaults.

---

## Validation

```bash
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.3.0 -p:DebugType=None -p:DebugSymbols=false
```
