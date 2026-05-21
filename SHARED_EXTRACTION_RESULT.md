# Shared Extraction Result — LSPDFRManager.Shared

**Date:** 2026-05-19  
**Branch:** main  
**Result:** Complete — 0 build errors, 914/914 tests pass.

---

## 1. Shared Project Created

| Item | Value |
|------|-------|
| Project file | `LSPDFRManager.Shared/LSPDFRManager.Shared.csproj` |
| Target framework | `net8.0` (no WPF, no Windows-specific dependencies) |
| Version | `3.7.17` / `3.7.17.0` (matches WPF project; required for `GetExecutingAssembly()` version detection in `UpdateCheckService`) |
| Packages | `SharpCompress 0.38.0` (archive extraction) |

---

## 2. Files Moved to LSPDFRManager.Shared

### Domain/ → LSPDFRManager.Shared/Domain/

All 88 domain model files. Pure C# records/classes with no WPF dependencies.

### Core/ → LSPDFRManager.Shared/Core/

| Moved | Original path |
|-------|--------------|
| `AppLogger.cs` | `Core/AppLogger.cs` |
| `Commands/IAppCommand.cs` | `Core/Commands/IAppCommand.cs` |
| `Features/IFeatureController.cs` | `Core/Features/IFeatureController.cs` |
| `Features/IFeatureModule.cs` | `Core/Features/IFeatureModule.cs` |
| `CarInstall/DiskSpaceValidator.cs` | `Core/CarInstall/DiskSpaceValidator.cs` |
| `CarInstall/IXmlPatcher.cs` | `Core/CarInstall/IXmlPatcher.cs` |
| `CarInstall/OpenIvInstallPlanner.cs` | `Core/CarInstall/OpenIvInstallPlanner.cs` |
| `CarInstall/OpenIvInstallPlanValidator.cs` | `Core/CarInstall/OpenIvInstallPlanValidator.cs` |
| `CarInstall/OpenIvExecutor.cs` | `Core/CarInstall/OpenIvExecutor.cs` |
| `CarInstall/XmlPatcher.cs` | `Core/CarInstall/XmlPatcher.cs` |
| `CarInstall/Models/CarInstallType.cs` | `Core/CarInstall/Models/CarInstallType.cs` |
| `CarInstall/Models/FileOperation.cs` | `Core/CarInstall/Models/FileOperation.cs` |
| `CarInstall/Models/OpenIvInstallPlan.cs` | `Core/CarInstall/Models/OpenIvInstallPlan.cs` |
| `CarInstall/Models/XmlPatch.cs` | `Core/CarInstall/Models/XmlPatch.cs` |

### Services/ → LSPDFRManager.Shared/Services/

79 service files moved; 17 WPF-bound services remain in WPF project.

Includes: `FileInstaller`, `BackupService`, `SmartInstallPlanner`, `TransactionService`,
`PathSafety`, `AppDataPaths`, `JsonFileStore`, `IArchive`, `ModDetector`,
`DiagnosticsOrchestrator`, `CleanupApplyService`, `RestorePointService`, `OivService`,
and ~70 others covering the full business logic surface.

---

## 3. Files Intentionally Left in WPF Project

### Core/ (WPF project root)

| File | Reason |
|------|--------|
| `Core/UiDispatcher.cs` | Directly wraps `System.Windows.Threading.Dispatcher` and `Application.Current` |
| `Core/Commands/AsyncAppCommand.cs` | Calls `UiDispatcher.BeginInvoke()` |
| `Core/InstallQueue.cs` | References `ModLibraryService` which depends on `UiDispatcher` |

### Services/ (WPF project root)

| File | Reason |
|------|--------|
| `Services/IUserPromptService.cs` | `System.Windows.MessageBox`, `MessageBoxButton/Image`; `Microsoft.Win32.OpenFileDialog` |
| `Services/OpenFileDialogService.cs` | `Microsoft.Win32.OpenFileDialog` (requires WPF) |
| `Services/IFileDialogService.cs` | Interface for WPF dialog service |
| `Services/ModLibraryService.cs` | Calls `UiDispatcher.Invoke()`; `ObservableCollection` for WPF binding |
| `Services/ModDownloadBridge.cs` | Calls `UiDispatcher.Invoke()`; refs `Features.Install` WPF namespace |
| `Services/LspdfrStatusService.cs` | `INotifyPropertyChanged` for WPF binding |
| `Services/DashboardStatusService.cs` | `INotifyPropertyChanged` + refs `ModLibraryService` |
| `Services/ConfigManagerService.cs` | `ObservableCollection` for WPF binding |
| `Services/BackupScheduler.cs` | References `ModLibraryService` |
| `Services/BatchReinstallService.cs` | References `InstallQueue` |
| `Services/ExportService.cs` | References `ModLibraryService` |
| `Services/GtaBaselineService.cs` | References `ModLibraryService` |
| `Services/LoadoutManifestService.cs` | References `ModLibraryService` |
| `Services/ModDuplicateDetector.cs` | References `ModLibraryService` |
| `Services/ModHealthScoringService.cs` | References `ModLibraryService` |
| `Services/ProfileManager.cs` | References `ModLibraryService` |
| `Services/SupportBundleService.cs` | References `ModLibraryService` |
| `Services/PatrolReadinessService.cs` | References `GtaBaselineService` (which refs `ModLibraryService`) |

---

## 4. Project References Added

| From | To |
|------|----|
| `LSPDFRManager.csproj` (WPF) | `LSPDFRManager.Shared` |
| `LSPDFRManager.LocalApi.csproj` | `LSPDFRManager.Shared` |
| `LSPDFRManager.Tests.csproj` | `LSPDFRManager.Shared` |

---

## 5. Additional Changes Required

### LSPDFRManager.csproj — exclusion globs updated

Added `LSPDFRManager.Shared\**` to the Compile/EmbeddedResource/None/Page Remove lists
so the WPF project's glob doesn't pick up Shared source files.

### GlobalUsings.cs added to Shared

The WPF project had `GlobalUsings.cs` with:
- `global using System.IO;`
- `global using System.IO.Compression;`
- `global using System.Collections.ObjectModel;`
- `global using System.Text.Json;`

An identical file was added to `LSPDFRManager.Shared/GlobalUsings.cs` so that moved
files (`JsonFileStore`, `FileInstaller`) compile without needing explicit using directives.
Also granted `InternalsVisibleTo` for `LSPDFRManager.Tests` and `LSPDFRManager`.

### XAML namespace assembly qualifiers

Three WPF XAML views referenced Domain types without an `assembly=` qualifier:
- `Views/ConfigView.xaml`
- `Views/OivView.xaml`
- `Views/PatrolReadinessDashboardView.xaml`

Updated from `clr-namespace:LSPDFRManager.Domain` to
`clr-namespace:LSPDFRManager.Domain;assembly=LSPDFRManager.Shared`.

### Version in Shared project

`UpdateCheckService.cs` uses `Assembly.GetExecutingAssembly()` to read the app version.
After the move, the executing assembly is `LSPDFRManager.Shared` (version 1.0.0 by default),
breaking 2 version-related tests. Fixed by setting `<Version>3.7.17</Version>` in the
Shared csproj to match the WPF project.

> **Future note:** When bumping the app version, update both `LSPDFRManager.csproj` and
> `LSPDFRManager.Shared/LSPDFRManager.Shared.csproj`. A `Directory.Build.props` could
> centralize this in Milestone 15+.

---

## 6. Commands Run and Results

| Command | Result |
|---------|--------|
| `dotnet restore LSPDFRManager.sln` | Pass |
| `dotnet build LSPDFRManager.sln --configuration Release` | Pass — 0 errors, 6 pre-existing warnings |
| `dotnet test LSPDFRManager.Tests/...` | Pass — 914/914 |

---

## 7. Remaining Extraction Opportunities

The following files remain in the WPF project but could move to Shared in a future
milestone after light refactoring:

| File | What's needed to move |
|------|-----------------------|
| `ModLibraryService.cs` | Extract `UiDispatcher.Invoke()` calls behind an abstraction (e.g. `IDispatcher`) |
| `ConfigManagerService.cs` | Replace `ObservableCollection` with `List<T>` or expose via interface |
| `LspdfrStatusService.cs` | Remove `INotifyPropertyChanged` dependency (not needed in LocalApi) |
| `DashboardStatusService.cs` | Same as above; also remove `ModLibraryService` dependency |
| `InstallQueue.cs` | Decouple from `ModLibraryService`; replace with LocalApi job queue pattern |

These are not required now. `LSPDFRManager.LocalApi` already has access to all the
business logic it needs via the 180+ files now in `LSPDFRManager.Shared`.

---

## 8. How This Prepares the App for React + TypeScript UI

`LSPDFRManager.LocalApi` (targeting `net8.0`) now references `LSPDFRManager.Shared` and can call:

- **`PathSafety.GetSafePath()`** — server-side path traversal protection for all React inputs
- **`FileInstaller.InstallAsync()`** — safe archive extraction with rollback
- **`BackupService.CreateBackupAsync()`** — backup creation before destructive operations
- **`TransactionService.RollbackAsync()`** — rollback on failure
- **`AppDataPaths.*`** — %APPDATA% persistence locations
- **Domain models** — all C# types can be JSON-serialized directly as API responses

React sends HTTP requests to LocalApi. LocalApi validates inputs (PathSafety), calls the
shared C# services, and returns JSON. The filesystem safety invariants remain entirely in
the shared C# layer — never exposed to JavaScript.
