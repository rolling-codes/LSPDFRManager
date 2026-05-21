# Core Workflows

## Install Flow

1. User drag-drop or browse archive → InstallView / InstallViewModel
2. ModDetector runs detection (file paths, extensions, archive name keywords) → confidence score
3. SmartInstallPlanner builds an InstallPlan: resolves conflicts, orders files, surfaces overwrite risks
4. User confirms type + author → FileInstaller extracts to GTA path via InstallPlan entries
5. InstallQueue enqueues install, processes asynchronously
6. Installed files tracked in InstalledMod → ModLibraryService saves to library.json

## Enable/Disable

- Rename installed files with `.disabled` suffix (e.g., `plugin.dll` → `plugin.dll.disabled`)
- GTA V/LSPDFR ignore `.disabled` files
- Toggled via ModLibraryService.SetEnabled()
- May fail silently if GTA V holds the file handle; check app.log for rename errors

## Detection Scoring

- ModDetector analyzes path patterns, file extensions, archive names
- Scores each ModType (LSPDFR Plugin, DLC, Replace, ASI, Script, EUP, Map, Sound) with confidence 0–100
- Low confidence (< 50) warns user; user can override
- Keywords in archive name can boost/flip detection (e.g., "dlcpack" → DLC)

## Backup/Restore

- BackupService ZIPs library.json, configs.json, key file copies
- RestoreService extracts snapshot, restores app state

## Smart Install Planner (v3.7.5+)

- `SmartInstallPlanner.BuildPlan()` — detects conflicts, orders files by priority, surfaces overwrite risks before any file is written
- `InstallerSafetyPolicy` — all per-plugin install rules live here (StopThePed, UltimateBackup, shared DLL ordering)
- `InstallPlan` / `InstallPlanEntry` carry per-file `InstallOverwriteRisk` and `InstallConflictAction`

## Setup Doctor / Diagnostics (v3.7.5+)

- `SetupDoctorService.RunAsync()` — checks GTA path, file presence, OneDrive paths, write permissions, keybind conflicts, version drift
- `RecipeValidatorService` — verifies required files for known plugins (ELS, UB, LSPDFR, RPH) are in correct locations
- `ConfigDiscoveryService` / `IniParser` — discovers all `.ini`/`.xml` configs; `PresetPatchService` applies backup-first patches with preview
- `KeybindConflictScanner` — detects duplicate keybinds across plugin INI files with severity grading
- `BackupEasyEditorService` / `BackupXmlParser` — validates and previews Ultimate Backup XML uniform patches; never writes without a `.bak`

## EUP Outfit Discovery (v3.7.5+)

- `EupOutfitDiscoveryService` scans installed EUP mods; `EupInferenceHelper` infers gender/unit from path patterns
- Domain: `EupUniformDefinition`, `EupGender`, `BackupUnitDefinition`, `BackupUniformMapping`
