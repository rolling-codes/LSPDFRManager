# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Build & Run

**Build (Debug)**
```bash
dotnet build LSPDFRManager.sln
```

**Run (requires GTA V path configured in settings)**
```bash
dotnet run --project LSPDFRManager.csproj
```

**Build self-contained executable (Release)**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

**Build release ZIP (framework-dependent)** — update version number as needed
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.7.13 -p:DebugType=None -p:DebugSymbols=false
New-Item -ItemType Directory -Path release-package/LSPDFRManager-v3.7.13 -Force
Copy-Item -Path publish/v3.7.13/* -Destination release-package/LSPDFRManager-v3.7.13 -Recurse
Compress-Archive -Path release-package/LSPDFRManager-v3.7.13 -DestinationPath LSPDFRManager-v3.7.13-win-x64.zip
```

> **If `dotnet publish` fails with WPF temp-file copy errors** (race with the IDE holding `obj/`), use msbuild directly:
> ```bash
> dotnet msbuild LSPDFRManager.csproj -t:Publish -p:Configuration=Release -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PublishDir=publish/v3.7.13 -p:DebugType=None -p:DebugSymbols=false
> ```

## Testing

**Run all tests**
```bash
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
```

> If `dotnet test` fails with a coverage file lock error (`msCoverageSourceRootsMapping` cannot be read), the VS Code test extension is holding the file. Build with msbuild first, then run with `--no-build`:
> ```bash
> dotnet msbuild LSPDFRManager.Tests/LSPDFRManager.Tests.csproj -p:CollectCoverage=false -v:q
> dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --no-build
> ```

**Run single test class**
```bash
dotnet test --filter "ClassName"
```

**Run with verbose output**
```bash
dotnet test -v detailed
```

## Code Conventions

- File-scoped namespaces: `namespace LSPDFRManager.ViewModels;` (no braces)
- Nullable reference types enabled: `string?` for nullable, `string` for non-null
- Singletons (`AppConfig`, `ModLibraryService`, `TransactionService`) must be reset in tests — call `Instance = null` / `AppDataPaths.OverrideRoot()` before and after each test class
- No mocking of file I/O or singletons in tests — use real temp directories and JSON serialization
- Tests that touch `AppConfig` or `AppDataPaths` must use `CommandCenterTestBase` (in `TestBase.cs`) or the `[Collection("CommandCenter")]` attribute — singleton tests are serialized to prevent races
- Architecture boundaries are enforced by `ArchitectureGuardTests`: ViewModels may not call `.Enqueue(`, `.EnqueueAsync(`, or `FileInstaller.` directly

## Installer Safety (Hard Constraint)

The installer must **never** leave the filesystem in a partially-installed state. The extraction sequence is non-negotiable:
1. `PathSafety.GetSafePath()` first
2. Create directory
3. Copy stream → file
4. Add to rollback list **only after** successful copy

External archive libraries (SharpCompress) must stay behind the `IArchive`/`IArchiveEntry` adapter boundary — no SharpCompress types outside the adapter layer. Use `FakeArchive`/`FakeArchiveEntry`/`ThrowingStream` for unit tests; real archives are Phase B only.

## Verification (Hard Constraint)

**Never claim a fix or release is complete based on unit tests alone.** Always build and launch the executable to confirm it starts and the UI renders without crashing:

```bash
dotnet build LSPDFRManager.sln
dotnet run --project LSPDFRManager.csproj
```

For UI-affecting changes, run the [UI smoke checklist](docs/ui-smoke-pr-check.md) before marking done.

## Release Packaging

When assembling a release ZIP, include only runtime artifacts — strip `build/`, `obj/`, `publish/`, WebView2 cache directories, and source files. Verify ZIP contents and size before publishing. Use the framework-dependent build above; confirm the unpacked folder launches cleanly before tagging.

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Current release notes: [RELEASE_v3.7.13.md](RELEASE_v3.7.13.md)

## New Feature Slices

Scaffold a new feature slice with:
```powershell
.\tools\New-FeatureSlice.ps1 -Name <FeatureName>
.\tools\New-FeatureSlice.ps1 -Name <FeatureName> -WithArchitectureTest
```

Structure: `Features/<Name>/I<Name>Controller.cs`, `<Name>WorkflowController.cs`, `<Name>FeatureModule.cs`. See [docs/feature-slice-template.md](docs/feature-slice-template.md) for the full skeleton.

## Smart-Feature Platform

LSPDFR Manager v3.7.13 includes a smart-feature platform for rule-based, testable, diagnosable features. Before adding a new smart feature, check whether it belongs in the existing feature-flag, rule-engine, diagnostics, support-bundle, or controller orchestration patterns instead of creating a new standalone service.

Key platform pieces:

- `FeatureFlagService` — controls non-trivial and experimental features.
- Rule engine — implements deterministic smart checks through testable rules.
- `IniLinterService` — validates `.ini`, `.cfg`, `.xml`, `.json`, and `.meta` configuration files.
- `DllDuplicateScanner` — detects duplicate/shared dependency DLLs across install locations.
- `ModHealthScoringService` — produces per-mod health verdicts.
- `SupportBundleService` — exports sanitized diagnostic bundles for triage.
- `PatrolReadinessController` — orchestrates readiness scoring and issue aggregation.
- `SafeModeController` — orchestrates safe-launch planning and execution.

Do not bypass these systems when adding related functionality. New smart features should be feature-flagged, rule-based where appropriate, emit diagnostics, be represented in support bundles when useful, and keep orchestration in controllers rather than ViewModels.

## Release EXE Gate

Every milestone ends with a release EXE validation pass. The release is not done until the built EXE launches cleanly, core dashboards work, support bundle export works, and the artifact is attached to the GitHub release.

Full checklist: [docs/release-gate.md](docs/release-gate.md)

**Quick sequence:**
1. Freeze feature work
2. `dotnet test` — all pass, including `ArchitectureGuardTests`
3. Build Release and package artifact
4. Launch the packaged EXE on a clean machine — verify startup, navigation, Patrol Readiness, Diagnostics, Support Bundle export
5. Tag, upload artifact to GitHub release, confirm milestone issues are closed

**If there is a problem — do not keep stacking changes.** Fix only the release blocker, rebuild the EXE, retest the artifact, update release notes, then ship.

## Focus Files

@docs/architecture.md
@docs/workflows.md
@docs/installer-safety.md
@docs/common-tasks.md
@docs/troubleshooting.md
@docs/developer-experience.md
@docs/release-gate.md
