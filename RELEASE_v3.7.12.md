# Release v3.7.12 - Smart Foundation + Patrol Readiness Dashboard

Feature release adding rule-based diagnostics, a feature flag system, and the Patrol Readiness Dashboard, the first user-facing surface of the smart-feature platform.

---

## Headline Feature

### Patrol Readiness Dashboard

A pre-flight check that tells you whether your LSPDFR setup is ready to launch and what to fix if it is not.

**Status levels:**

| Status | Meaning |
|--------|---------|
| Ready for Patrol | No blocking issues, no warnings |
| Needs Attention | Warnings present but nothing blocking |
| Not Ready | One or more blocking issues must be resolved |

**What it checks:**
- GTA V path, GTA5.exe, RAGEPluginHook.exe, and LSPDFR.dll presence
- Mod health per mod
- Duplicate shared DLLs
- Config file lint errors in `.ini`, `.xml`, `.json`, `.cfg`, and `.meta` files
- Plugin and config changes since the last known-good launch
- ScriptHookV and ScriptHookVDotNet version drift warnings

**Actions on the dashboard:**
- **Scan Now** - runs all checks and scores the result from 0 to 100
- **Mark as Known-Good** - snapshots plugin list and config hashes as a reference baseline
- **Export Support Bundle** - produces a sanitized ZIP for troubleshooting

---

## Smart-Feature Foundation

- `FeatureFlagService` provides file-backed feature overrides at `%APPDATA%\LSPDFRManager\feature-flags.json`
- Rule engine primitives support deterministic, testable checks with suggested fixes
- `IniLinterService` validates common LSPDFR configuration file formats
- `DllDuplicateScanner` finds duplicate shared dependencies across install locations
- `ModHealthScoringService` produces per-mod health verdicts
- `SupportBundleService` exports sanitized diagnostics for triage
- `PatrolReadinessController` orchestrates readiness scoring and issue aggregation

---

## UI Changes

- **Patrol Ready** nav item added above Diagnostics in the sidebar
- **Dev Diagnostics** nav item added at the bottom of the sidebar
- **Settings** includes UI scale RadioButtons
- **Config tab** shows mod name above the file name in parsed and raw editor headers

---

## Fixes

- RAGE Plugin Hook archives are detected with a dedicated rule instead of low-confidence miscellaneous detection.
- ASI archive detection remains covered for plain trainer-style `.asi` packages.
- Uninstall confirmation dialogs now use the main window as owner so they do not appear behind the app.

---

## Architecture

| Layer | Added |
|-------|-------|
| Domain | `InstallIssue`, `PatrolReadinessSummary`, `FeatureManifest`, `FeatureStage`, rule models, lint results, DLL duplicate results, mod health results, known-good diff support |
| Services | `FeatureFlagService`, `FeatureRegistry`, `IniLinterService`, `DllDuplicateScanner`, `ModHealthScoringService`, `InstallReceiptService`, `SupportBundleService`, `RageLogParser`, `RageLogScanner` |
| Features | `PatrolReadiness/IPatrolReadinessController`, `PatrolReadiness/PatrolReadinessController` |
| ViewModels | `PatrolReadinessDashboardViewModel`, `DevDiagnosticsViewModel` |
| Views | `PatrolReadinessDashboardView.xaml`, `DevDiagnosticsView.xaml` |
| Tests | 806 passing |

---

## Verification

```
dotnet build LSPDFRManager.sln
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
```

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.11...v3.7.12
