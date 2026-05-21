# Release v3.7.10 — Control Layer, OIV Templates & Updates Slice

## Summary

This release ships three major feature areas behind a lightweight control-layer architecture, plus lifecycle hygiene fixes, testability improvements, and tooling to make future features fast to add.

---

## Bug Fixes

- **`AppLogger.Warning` typo** — `AppLogger.Warn` (non-existent) in `OivViewModel` was replaced with the correct `AppLogger.Warning`, resolving a build error introduced in the OIV creator pipeline.
- **Installer failure classification** — `FileNotFoundException` / `DirectoryNotFoundException` now correctly surface as `MissingFile`; generic `IOException` surfaces as `InvalidArchive` with a user-actionable message. Ordering is preserved (specific before generic).
- **WebView ZIP staging** — browser downloads no longer trigger installs automatically. A ZIP staged via Browse is held for explicit user confirmation in the Install tab before any file is written.
- **ViewModel lifecycle** — `InstallViewModel` and `BrowseViewModel` now detach from `ModDownloadBridge` on dispose, preventing ghost subscriptions when views are closed and reopened.

---

## New Features

### Control Layer Foundation

Lightweight feature-slice abstractions added to `Core/`:

- `IAppCommand` / `AsyncAppCommand` — cancellable, observable commands that disable themselves while running.
- `IFeatureController` / `IFeatureModule` — standard interfaces for feature slices; each slice owns its controller and registers commands.

### Install Feature Slice

- `IInstallController` + `InstallWorkflowController` own detection, staging, plan building, and confirm-install orchestration.
- `InstallViewModel` delegates all install workflow steps to the controller; it no longer calls `ModDetector`, `SmartInstallPlanner`, or `InstallQueue` directly.
- `ConfirmedInstall` model carries the result of a user-confirmed install intent.

### Library Feature Slice

- `ILibraryController` + `LibraryWorkflowController` own bulk enable/disable and undo-restoration workflows.
- `LibraryViewModel` delegates bulk toggle operations to the controller.
- `BulkToggleState` model carries the snapshot needed for undo.

### OIV Creator Templates (Feature #37)

- New `Features/OivCreatorTemplates/` slice with `IOivTemplateController` and `OivTemplateController`.
- Template selection has **zero side effects** — metadata and files are unchanged until the user clicks **Apply**.
- **Apply** builds an immutable `OivTemplateApplyPlan` from a snapshot DTO and mutates only non-user-edited fields (`IsUserEdited` flag respected per `OivFileEntry`).
- **Undo** restores the pre-apply snapshot (one level).
- **Add File after Apply** suggests a safe install path (editable); `PathSafety.GetSafePath` validates all suggestions.
- `OivView.xaml` gains a "TEMPLATE (OPTIONAL)" section with Apply/Undo buttons and a `TemplateApplyStatus` feedback label.

### Updates Slice — Phase 1

- New `Features/Updates/` slice with `IUpdateController`, `UpdateWorkflowController`, and `UpdateFeatureModule`.
- `SettingsViewModel.CheckForUpdatesCommand` delegates to `IUpdateController.CheckForUpdatesAsync`; no download or apply logic exists anywhere in the controller or ViewModel.
- `UpdateCheckResult` domain type carries version, availability flag, release URL, and offline status.
- `UpdateCheckService.CheckAsync` is now `virtual` to support test subclassing without a full mock framework.

---

## Other Updates

### Testability (PR4 Dialog Abstraction)

- `IFileDialogService` / `OpenFileDialogService` added to `Services/`; `OivViewModel.AddCreatorFile` uses the interface.
- Suggest-on-add tests are now fully deterministic via `FakeFileDialogService` — no Win32 dialog involvement.

### Architecture Guard Tests

- `ArchitectureGuardTests` enforce that ViewModels do not enqueue installs or call `FileInstaller` directly; allowlist covers the legacy batch-reinstall path.
- `ControlLayerTests` cover: `AsyncAppCommand` disables while running; `InstallViewModel` delegates detect/review to controller; `BrowseViewModel` and `InstallViewModel` dispose correctly.

### Feature Slice Tooling

- `tools/New-FeatureSlice.ps1` scaffolds a new feature slice (controller, module, interface, models, tests) in one command. Supports `-WithArchitectureTest` flag.
- `Features/_Template/README.md` documents the copy/pasteable slice pattern.
- `docs/feature-slice-template.md` added.
- `docs/architecture.md`, `docs/common-tasks.md`, `docs/dependencies.md`, and `docs/ui-smoke-pr-check.md` updated to reflect new patterns and triage notes.

### Docs & Process

- `CLAUDE.md` gains a **Verification (Hard Constraint)** section requiring the executable to be launched before claiming a fix is complete, and a **Release Packaging** section listing what to strip from release ZIPs.
- `.github/pull_request_template.md` added with UI smoke checklist fields.
- `AGENTS.md` added for agentic worker instructions.

---

## Verification

```
dotnet build LSPDFRManager.sln          # 0 errors
dotnet test LSPDFRManager.Tests/...     # 734/734 passed
dotnet test --filter "OivTemplate|OivViewModel|OivCreator|ArchitectureGuard|UpdateWorkflow"  # 63/63 passed
```

Manual WPF/WebView smoke run required before production use — see [docs/ui-smoke-pr-check.md](docs/ui-smoke-pr-check.md).
