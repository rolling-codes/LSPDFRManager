# Architecture

**MVVM WPF Desktop App** (.NET 8, Windows x64 only)

## Layer Structure

- **Views/** — WPF UserControls, one per tab: Dashboard, Library, Install, Browse, Diagnostics, Profiles, Backups, History, Logs, Settings, Config, SetupWizard; plus `Views/Components/` for shared controls
- **ViewModels/** — MVVM view models; MainViewModel orchestrates tab navigation; each tab has a dedicated VM
- **Domain/** — Data classes (InstalledMod, ModInfo, ModType, AppConfig, ModManifest, InstallResult, RestorePoint, ModProfile, etc.)
- **Services/** — Business logic (~42 services: ModLibraryService, ModDetector, FileInstaller, BackupService, ProfileManager, CrashLogAnalyzer, DependencyScanner, DiagnosticsOrchestrator, etc.)
- **Core/** — AppLogger (file logging), InstallQueue (background async install processor), UiDispatcher, `Core/CarInstall/` subfolder
- **Core/Commands/** and **Core/Features/** — lightweight control-layer abstractions (`IAppCommand`, `IFeatureController`, `IFeatureModule`) for feature slices.
- **Features/** — opt-in feature slices. New orchestration goes behind feature controllers before ViewModels call Services.
- **Converters/** — WPF value converters (InverseBoolConverter, StringToBrushConverter, StringToVisibilityConverter)
- **LSPDFRManager.Api/** — Separate API service sub-project (Models/, Services/)

## Key Singletons

- **ModLibraryService** — In-memory registry of all installed mods, synced to `library.json`. Search, enable/disable, conflict detection.
- **AppConfig** — App settings (GTA path, backup folder, etc.), synced to `config.json`.
- **LspdfrStatusService** — Monitors live status of LSPDFR/GTA V processes (shown in sidebar).
- **InstallQueue** — Background task processor; installs one mod at a time to avoid file conflicts.
- **DiagnosticsOrchestrator** — Aggregates findings from all scanner services; deduplicates by Title+Path+Severity.

Singletons use lazy-init. Modifications (Add, Remove, SetEnabled) trigger immediate Save to JSON. No locks — assume single-threaded UI access.

## Storage

All data → `%APPDATA%\LSPDFRManager\`:
- `library.json` — Installed mods registry
- `configs.json` — Captured mod config snapshots
- `config.json` — App settings
- `keys/` — Cached key files
- `Backups/` — ZIP archives
- `app.log` — Runtime log

## MVVM Patterns

- [ObservableObject.cs](../ViewModels/ObservableObject.cs) — base class for VMs, implements INotifyPropertyChanged
- [RelayCommand.cs](../ViewModels/RelayCommand.cs) — simple ICommand for button actions
- Feature controllers own multi-step workflows. ViewModels own bindable state, validation presentation, and command delegation.
- New install/write/download orchestration must live behind a controller or explicit command. ViewModels must not enqueue installs directly.
- See [feature-slice-template.md](feature-slice-template.md) for the copy/pasteable feature-slice pattern.
- [Resources/Styles.xaml](../Resources/Styles.xaml) — centralized dark theme (brushes, templates)
- [GlobalUsings.cs](../GlobalUsings.cs) — cross-cutting imports (System.IO, System.IO.Compression, System.Collections.ObjectModel, System.Text.Json)
- File-scoped namespaces: `namespace LSPDFRManager.ViewModels;` (no braces)
- Nullable reference types enabled: `string?` for nullable, `string` for non-null

## How To Review Flow Migrations

**Where does orchestration live now?** Feature controllers own workflow orchestration. ViewModels should delegate to controllers, update bindable state, and present validation or progress.

**What should reviewers grep for?** Search for `Enqueue`, installer service calls, and event subscriptions. Enqueue/install side effects should happen only after explicit user confirmation, and event subscriptions should have matching cleanup when the subscribing object has a lifecycle.

**How are UI dialogs handled?** New UI dialogs must go through a dialog service if the flow requires unit tests.

**What tests enforce it?** `ArchitectureGuardTests` protect side-effect boundaries, while lifecycle regression tests cover duplicate subscriptions, double staging, and double prompts.

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| SharpCompress | 0.38.0 | RAR, 7-Zip archive extraction (ZIP built-in) |
| Microsoft.Web.WebView2 | 1.0.2739.15 | Embedded browser (lcpdfr.com BrowseView) |
| xUnit | 2.9.0 | Unit testing (test project only) |

Dependency triage notes live in [dependencies.md](dependencies.md).
