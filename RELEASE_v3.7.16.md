# Release v3.7.16 - Setup, Safe Mode, and Cleanup Fixes

## What's New

### First-Launch Setup Wizard
- Fixed first-launch setup flow so users can select and scan their GTA V root before the main UI loads.
- Wizard is shown automatically on first run or when no GTA path is configured; skipped on subsequent launches.

### Navigation
- Added scrollable sidebar navigation so all nav buttons remain accessible at any window height.

### LSPDFR / RPH Detection Fixes
- Fixed LSPDFR core detection to correctly identify `Plugins/LSPD First Response.dll`.
- Fixed RAGE Plugin Hook detection to include `RagePluginHook.dll` alongside `RAGEPluginHook.exe`.
- Added `Albo1125.Common.dll` as a recognised shared dependency (not classified as a removable plugin).

### Safe Mode Builder
- Fixed Safe Mode Builder navigation so the wizard pages advance and return correctly.
- Hardened apply safety: config backups are always written before any patch is applied.

### Safe LSPDFR Cleanup Tool
- New Cleanup tab with a four-screen wizard: mode select → preview → confirm → result.
- **Safe Core Reset mode**: default-selects only the LSPDFR core DLL; third-party plugins are never selected.
- **Third-Party Plugin Cleanup mode**: nothing selected by default; requires typing `DELETE SELECTED PLUGINS` to confirm.
- Preview shows every candidate grouped by plugin with risk labels before any deletion occurs.
- A timestamped ZIP backup is created before any file is deleted; the operation is aborted if the backup fails.
- GTA executables and paths outside the GTA root are always blocked from deletion.

## Quality

- Build: 0 errors, 0 warnings.
- Tests: all tests passing.
