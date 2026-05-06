# Refactor Summary

## What changed
- Added shared app-data path and JSON storage helpers.
- Simplified persistent services:
  - `ModLibraryService`
  - `ConfigManagerService`
  - `BackupService`
  - `ExportService`
  - `BatchReinstallService`
- Added `InstalledModFileService` to centralize enable/disable/uninstall file operations.
- Reworked `InstallQueue` to support queued completion tracking and clearer install flow.
- Reworked major view models:
  - `MainViewModel`
  - `InstallViewModel`
  - `BrowseViewModel`
  - `LibraryViewModel`
  - `SettingsViewModel`
  - `ModItemViewModel`
- Fixed broken UI contract issues where XAML expected properties/commands that the view models did not expose.

## New files
- `Services/AppDataPaths.cs`
- `Services/JsonFileStore.cs`
- `Services/InstalledModFileService.cs`

## Notes
- The environment here did not allow running the .NET SDK, so this was a source-level refactor and sanity pass rather than a compiled/tested build.
