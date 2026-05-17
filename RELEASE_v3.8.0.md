# Release v3.8.0 — Smart Foundation + Patrol Readiness Dashboard

Feature release adding rule-based diagnostics, a feature flag system, and the Patrol Readiness Dashboard — the first user-facing surface of the smart-feature platform.

---

## Headline Feature

### Patrol Readiness Dashboard

A pre-flight check that tells you exactly whether your LSPDFR setup is ready to launch — and what to fix if it is not.

**Status levels:**

| Status | Meaning |
|--------|---------|
| ✅ Ready for Patrol | No blocking issues, no warnings |
| ⚠ Needs Attention | Warnings present but nothing blocking |
| 🚫 Not Ready | One or more blocking issues must be resolved |

**What it checks (all deterministic, no AI):**
- GTA V path, GTA5.exe, RAGEPluginHook.exe, LSPDFR.dll presence
- Mod health per-mod (Healthy / Needs Attention / Broken)
- Duplicate shared DLLs (RAGENativeUI, LemonUI, Newtonsoft.Json, etc.)
- Config file lint errors (`.ini`, `.xml`, `.json`, `.cfg`, `.meta`)
- Plugin and config changes since the last known-good launch
- ScriptHookV / ScriptHookVDotNet version drift warnings

**Actions on the dashboard:**
- **Scan Now** — runs all checks and scores the result (0–100)
- **Mark as Known-Good** — snapshots the current plugin list and config hashes as a reference baseline
- **Export Support Bundle** — produces a sanitized ZIP for sharing when troubleshooting

Score formula: `100 - (blockers × 20) - (warnings × 5)`, floor 0.

---

## Smart-Feature Foundation (developer + power-user)

### Feature Flag System
- `IFeatureFlagService` / `FeatureFlagService` — file-backed JSON overrides at `%APPDATA%\LSPDFRManager\feature-flags.json`
- 12 registered features across Stable / Preview / Experimental / DevOnly lifecycle stages
- Experimental features are disabled by default; Stable and Preview features are on

### Rule Engine
- `IRule<TContext>` interface, `RuleResult`, `SuggestedFix`, `FixRisk` domain types
- All smart behavior is deterministic, individually testable, and explainable
- Rules state what they found, why it matters, and what the user should do

### Config Linter (`IniLinterService`)
- Lints `.ini`, `.cfg`, `.xml`, `.meta`, `.json` config files
- Detects: duplicate keys, empty values, bad booleans, malformed lines, invalid XML/JSON structure
- Error-level lint findings become blocking issues in Patrol Readiness; warnings stay as warnings

### DLL Duplicate Scanner (`DllDuplicateScanner`)
- Scans GTA root, `plugins/`, `plugins/LSPDFR/`, `scripts/` for the same DLL in multiple locations
- Flags known shared dependencies (RAGENativeUI.dll, LemonUI.RAGE.dll, Newtonsoft.Json.dll, NAudio.dll, etc.) at higher severity
- Results feed Diagnostics Center and Patrol Readiness

### Mod Health Scoring (`ModHealthScoringService`)
- Per-mod `Healthy / Needs Attention / Broken / Unknown` verdict
- Aggregates diagnostic findings by install path, mod name, and installed file names
- Disabled mods automatically score as Unknown

### Known-Good Baseline (extended)
- `GtaBaselineService.MarkKnownGood()` — snapshots enabled plugin paths and SHA-256 hashes of all config files
- `DiffCurrentVsKnownGood()` — returns added/removed plugins and changed configs since the snapshot
- Baseline stored in `%APPDATA%\LSPDFRManager\gta_baseline.json`

### Install Receipt (`InstallReceiptService`)
- Generates a text or JSON receipt from any `InstallTransaction` record
- Shows mod name, install timestamp, files added, files overwritten, and DLC entry status

### Support Bundle Export (`SupportBundleService`)
- Produces a sanitized ZIP containing: `app-info.json`, `feature-flags.json`, `installed-mods.json`, `diagnostic-events.log` (last 2,000 lines), `change-history.json`, `backup-history.json`, `sanitized-paths.txt`, and recent RPH log files
- All user home paths replaced with `%USERPROFILE%` / `%APPDATA%` tokens
- No secrets, no raw personal paths

### Developer Diagnostics Page
- Shows all feature flags with per-flag Toggle / Reset controls
- Live app log tail (last 100 lines)
- Support Bundle export shortcut
- Accessible via "Dev Diagnostics" in the sidebar

---

## UI Changes

- **Patrol Ready** nav item added above Diagnostics in the sidebar
- **Dev Diagnostics** nav item added at the bottom of the sidebar
- **Settings** — UI scale RadioButtons (Small / Default / Large / Extra Large)
- **Config tab** — mod name shown above the file name in both parsed and raw editor headers

---

## Architecture

| Layer | Added |
|-------|-------|
| Domain | `InstallIssue`, `PatrolReadinessSummary`, `FeatureManifest`, `FeatureStage`, `RuleModels` (`IRule<T>`, `RuleResult`, `SuggestedFix`, `FixRisk`), `LintFinding`, `DllDuplicateResult`, `ModHealthStatus`, `ModHealthResult`, `KnownGoodDiff` (on `GtaBaseline`) |
| Services | `FeatureFlagService`, `FeatureRegistry`, `IniLinterService`, `DllDuplicateScanner`, `ModHealthScoringService`, `InstallReceiptService`, `SupportBundleService`, `RageLogParser`, `RageLogScanner` |
| Features | `PatrolReadiness/IPatrolReadinessController`, `PatrolReadiness/PatrolReadinessController` |
| ViewModels | `PatrolReadinessDashboardViewModel`, `DevDiagnosticsViewModel` |
| Views | `PatrolReadinessDashboardView.xaml`, `DevDiagnosticsView.xaml` |
| Tests | 806 passing (20 net new this release) |

---

## Verification

```
dotnet build LSPDFRManager.sln      # 0 errors
dotnet test LSPDFRManager.Tests/... # 806/806 passed
```

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.11...v3.8.0
