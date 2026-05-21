# Shared Extraction Analysis — LSPDFRManager.Shared

**Date:** 2026-05-19  
**Branch:** main  
**Baseline:** 914/914 tests pass, 0 build errors.

---

## 1. Goal

Create `LSPDFRManager.Shared` (targeting `net8.0`) containing all business logic that
is free of WPF/Windows-UI dependencies.  Both the WPF project and `LSPDFRManager.LocalApi`
will reference this shared library.

---

## 2. Classification

### 2.1 WPF-specific types found

| Type / Namespace | Source |
|-----------------|--------|
| `System.Windows.Threading.Dispatcher` / `Application.Current` | `Core/UiDispatcher.cs` |
| `System.Windows.MessageBox` / `MessageBoxButton` / `MessageBoxImage` | `Services/IUserPromptService.cs` |
| `Microsoft.Win32.OpenFileDialog` | `Services/OpenFileDialogService.cs` |
| `UiDispatcher.Invoke()` call | `Services/ModLibraryService.cs`, `Services/ModDownloadBridge.cs` |

### 2.2 WPF-pattern types (BCL but WPF-oriented)

| Type | Files | Notes |
|------|-------|-------|
| `ObservableCollection<T>` (BCL) | `ModLibraryService.cs`, `ConfigManagerService.cs` | Used for WPF data binding; `ModLibraryService` also calls `UiDispatcher` |
| `INotifyPropertyChanged` (BCL) | `LspdfrStatusService.cs`, `DashboardStatusService.cs` | Used for WPF data binding |

---

## 3. Files Safe to Move to LSPDFRManager.Shared

### 3.1 Domain/ — ALL 88 files

Pure record/class domain models. Zero WPF references. Zero service references.

### 3.2 Core/ — 13 files (of 13+3)

| File | Reason safe |
|------|------------|
| `Core/AppLogger.cs` | BCL + `AppDataPaths` (which also moves) |
| `Core/Commands/IAppCommand.cs` | Pure interface |
| `Core/Features/IFeatureController.cs` | Pure interface, refs `IAppCommand` |
| `Core/Features/IFeatureModule.cs` | Pure interface |
| `Core/CarInstall/DiskSpaceValidator.cs` | BCL only |
| `Core/CarInstall/IXmlPatcher.cs` | Pure interface |
| `Core/CarInstall/Models/CarInstallType.cs` | Pure enum/record |
| `Core/CarInstall/Models/FileOperation.cs` | Pure record |
| `Core/CarInstall/Models/OpenIvInstallPlan.cs` | Pure record |
| `Core/CarInstall/Models/XmlPatch.cs` | Pure record |
| `Core/CarInstall/OpenIvInstallPlanner.cs` | Domain + BCL |
| `Core/CarInstall/OpenIvInstallPlanValidator.cs` | Domain + BCL |
| `Core/CarInstall/OpenIvExecutor.cs` | Domain + BCL |
| `Core/CarInstall/XmlPatcher.cs` | Domain + BCL |

### 3.3 Services/ — 78 files (of 97)

All Services files that do NOT reference WPF types, `UiDispatcher`, `ModLibraryService`,
`ConfigManagerService`, `LspdfrStatusService`, `DashboardStatusService`,
`IUserPromptService`, `IFileDialogService`, `InstallQueue`, or WPF UI Features.

Includes: `FileInstaller`, `BackupService`, `SmartInstallPlanner`, `TransactionService`,
`PathSafety`, `AppDataPaths`, `JsonFileStore`, `IArchive`, `ModDetector` (doc-comment ref only),
and all other pure-C# services.

---

## 4. Files That Must Stay in WPF Project

### 4.1 Core — 3 files

| File | Reason |
|------|--------|
| `Core/UiDispatcher.cs` | Wraps `System.Windows.Threading.Dispatcher` |
| `Core/Commands/AsyncAppCommand.cs` | Calls `UiDispatcher.BeginInvoke()` |
| `Core/InstallQueue.cs` | References `ModLibraryService` (which uses `UiDispatcher`) |

### 4.2 Services — 19 files

| File | Reason |
|------|--------|
| `Services/IUserPromptService.cs` | `System.Windows.MessageBox`, `MessageBoxButton/Image` |
| `Services/OpenFileDialogService.cs` | `Microsoft.Win32.OpenFileDialog` (WPF) |
| `Services/IFileDialogService.cs` | Interface for WPF dialog service |
| `Services/ModLibraryService.cs` | Calls `UiDispatcher.Invoke()`; uses `ObservableCollection` for WPF binding |
| `Services/ModDownloadBridge.cs` | Calls `UiDispatcher.Invoke()`; refs WPF Features.Install namespace |
| `Services/LspdfrStatusService.cs` | `INotifyPropertyChanged` for WPF binding |
| `Services/DashboardStatusService.cs` | `INotifyPropertyChanged` + refs `ModLibraryService` |
| `Services/ConfigManagerService.cs` | `ObservableCollection` for WPF binding |
| `Services/ModLibraryService.cs` | (listed above) |
| `Services/BackupScheduler.cs` | References `ModLibraryService` |
| `Services/BatchReinstallService.cs` | References `InstallQueue` |
| `Services/ExportService.cs` | References `ModLibraryService` |
| `Services/GtaBaselineService.cs` | References `ModLibraryService` |
| `Services/LoadoutManifestService.cs` | References `ModLibraryService` |
| `Services/ModDuplicateDetector.cs` | References `ModLibraryService` |
| `Services/ModHealthScoringService.cs` | References `ModLibraryService` |
| `Services/ProfileManager.cs` | References `ModLibraryService` |
| `Services/SupportBundleService.cs` | References `ModLibraryService` |

---

## 5. Dependency Cycle Check

```
LSPDFRManager.Shared    ← no dependencies on WPF project (safe)
LSPDFRManager.csproj    → references LSPDFRManager.Shared (safe)
LSPDFRManager.LocalApi  → references LSPDFRManager.Shared (safe)
LSPDFRManager.Tests     → references LSPDFRManager.csproj (already does, unchanged)
                        → add reference to LSPDFRManager.Shared (for moved types)
```

No circular references.

---

## 6. Move Plan

1. Create `LSPDFRManager.Shared/LSPDFRManager.Shared.csproj` (net8.0, SharpCompress ref)
2. Add it to `LSPDFRManager.sln`
3. Move `Domain/` → `LSPDFRManager.Shared/Domain/`
4. Move safe `Services/` files → `LSPDFRManager.Shared/Services/`
5. Move safe `Core/` files → `LSPDFRManager.Shared/Core/`
6. Add `<ProjectReference>` in WPF project → Shared
7. Add `<ProjectReference>` in LocalApi project → Shared
8. Add `<ProjectReference>` in Tests project → Shared (if needed)
9. Verify WPF exclusion globs in `LSPDFRManager.csproj` cover the moved paths
10. Build + test

---

## 7. How This Supports the React UI

`LSPDFRManager.LocalApi` will expose shared business logic as HTTP endpoints:

- `Domain/` models → serialized as JSON responses (DTOs already exist in C#)
- `Services/PathSafety.cs` → called server-side to validate paths from React inputs
- `Services/FileInstaller.cs` → called from install API endpoints
- `Services/BackupService.cs` → called from backup API endpoints
- `Services/TransactionService.cs` → rollback support for all destructive operations

React never touches the filesystem directly. All filesystem safety invariants remain
in the shared C# layer, callable only through validated LocalApi endpoints.
