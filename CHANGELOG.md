# Changelog

All notable changes to this project are documented in this file.

## [1.2.0] - 2026-04-19

### Added
- Install preflight checks for common LSPDFR/GTA setup issues:
  - invalid GTA path
  - Program Files permission-risk warning
  - missing `RAGEPluginHook.exe`
  - missing LSPDFR core DLL
  - missing `ScriptHookV.dll` for Script/ASI mods
  - BattlEye reminder
- Install UI setup-check panel that surfaces preflight warnings before install.
- Library management improvements:
  - status filtering (enabled/disabled)
  - sorting controls
  - bulk enable/disable/toggle of visible mods
- Test coverage for file installer wrapper-folder stripping and preflight warning behavior.

### Changed
- File installation now strips a single shared wrapper top-level folder when present.
- File extraction now validates destination paths to block unsafe traversal payloads.

