# 8B — Safe LSPDFR Cleanup / Reinstall Helper

**Date:** 2026-05-18  
**Status:** Approved, implementing  
**Author:** Tom (rolling-codes)

---

## Overview

A preview-first, backup-before-delete cleanup tool that lets users remove LSPDFR core files,
RAGE Plugin Hook, and/or selected third-party plugins before a reinstall. Every deletion is
gated behind a backup ZIP and explicit confirmation.

---

## Architecture

Approach C: shared scan once, mode-specific pure filter/default logic.

```
LspdfrCleanupScanner.Scan(gtaRoot)        → CleanupScanResult
ICleanupMode.Apply(CleanupScanResult)     → CleanupModePreset
GtaFileBackupService.CreateCleanupBackupAsync(...)  → CleanupBackupResult
CleanupApplyService.ApplyAsync(...)       → CleanupApplyResult
```

---

## Modes (8B scope: 1 and 4 only)

| # | Name | Default selected | Typed confirm |
|---|---|---|---|
| 1 | Safe Core Reset | LSPDFR core DLL only | None (button) |
| 4 | Selected Third-Party Plugin Cleanup | Nothing | DELETE SELECTED PLUGINS |

Modes 2, 3, 5, 6 deferred to 8C.

---

## Domain Models

- `CandidateClassification` — LspdfrCore, LspdfrData, RphCore, ThirdPartyPlugin, PluginConfig, PluginDataFolder, SharedDependency, OptionalInfrastructure, ManualReview, Blocked
- `CleanupMode` — enum (6 values)
- `CleanupRiskLevel` — Low, Medium, High, Advanced
- `RemovalCandidate` — Id, RelativePath, FullPath, Classification, RiskLevel, Reason, IsDirectory, SizeBytes?
- `RemovalGroup` — Label, GroupKind, Candidates
- `CleanupScanResult` — GtaRoot, Groups, ScannedAt
- `CleanupModePreset` — Mode, Groups, DefaultSelectedIds, RiskLevel, WarningText, ConfirmPhrase?, RequireBackup
- `CleanupBackupResult` — Success, ZipPath?, FailedPaths, ErrorMessage?
- `CleanupApplyResult` — Success, DeletedPaths, FailedPaths, BackupZipPath?, AbortReason?

---

## Backup Contract

- ZIP: `lspdfr_cleanup_backup_<timestamp>.zip` in configured backup folder
- Preserves GTA-root-relative paths
- Includes `cleanup_manifest.json` (timestamp, gtaRoot, mode, appVersion, files list)
- Backup failure → abort with zero deletions, no partial state
- If ZIP creation or manifest write fails → abort

---

## Safety Invariants (non-negotiable)

1. Preview deletes nothing
2. Backup happens before deletion
3. Backup failure aborts with zero deletions
4. Apply deletes only selected candidates
5. ManualReview never selected by default
6. SharedDependency never selected by default
7. Third-party plugins never selected by Safe Core Reset
8. `plugins/lspdfr/` never wiped as part of LSPDFR Core
9. GTA executables are Blocked
10. Outside-root paths are Blocked
11. Confirmation required before apply

---

## UI Flow

Screen 1: Choose mode (Mode 1 or Mode 4)  
Screen 2: Scan + grouped preview with checkboxes  
Screen 3: Confirmation (typed phrase if required)  
Screen 4: Result report with backup ZIP path  

Always: Cancel/Back available. No deletion from screen 1 or 2.

---

## Tests (13)

1. Scanner classifies LSPDFR core DLL as LspdfrCore  
2. Scanner classifies lspdfr/ as LspdfrData  
3. Scanner classifies plugins/lspdfr/MyPlugin.dll as ThirdPartyPlugin  
4. Scanner groups MyPlugin.ini with MyPlugin.dll  
5. Scanner classifies Albo1125.Common.dll as SharedDependency  
6. Scanner blocks GTA executables (Blocked)  
7. Scanner blocks outside-root paths  
8. SafeCoreReset selects core DLL, not plugins/lspdfr contents  
9. SelectedThirdPartyPluginCleanup defaults to nothing selected  
10. Backup ZIP preserves GTA-root-relative paths  
11. Manifest included in ZIP  
12. Only selected candidates backed up  
13. Backup failure aborts with zero deletions  

---

## Deferred (8C)

- Modes 2, 3, 5, 6
- "Open backup folder" button
- Full restore-from-cleanup-backup
- Total size display
