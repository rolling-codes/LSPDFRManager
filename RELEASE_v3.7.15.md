# Release v3.7.15 - LSPDFR/RPH Detection and Safe Mode Fixes

---

## Changes

- Fixed LSPDFR core detection to recognize canonical `Plugins/LSPD First Response.dll` while tolerating the legacy `plugins/LSPDFR.dll` path.
- Fixed RAGE Plugin Hook detection to require root-level `RAGEPluginHook.exe` only (subdirectory candidates removed).
- Fixed RPH readiness and recipe validation to require `RagePluginHook.dll` alongside `RAGEPluginHook.exe`.
- Added `Albo1125.Common.dll` shared-dependency handling for the Albo1125 plugin family install ordering.
- Wired Safe Mode Builder into sidebar navigation and App.xaml DataTemplate.
- Fixed dashboard Safe Launch quick action to navigate to the Safe Mode Builder instead of applying changes silently.
- Hardened Safe Mode apply flow: backup failure now aborts before any file changes occur.
- Improved Safe Mode verification mismatch reporting: mismatches now set Success=false and surface a specific user message.
- Added focused regression tests for installer, locator, planner, and recipe validation.

## Verified

- Build: 0 errors.
- Tests: all pass.
