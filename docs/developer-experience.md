# Developer Experience Standard

Defines the bar for healthy feature work in LSPDFRManager. A feature is ready to merge when it satisfies every item in the checklist below. This doc covers structure, testing, diagnostics, and triage — not CI/CD or packaging.

---

## Feature Health Checklist

| # | Criterion | How to verify |
|---|-----------|--------------|
| 1 | Logic is outside the ViewModel | No `.Enqueue(`, `.EnqueueAsync(`, `FileInstaller.`, or file I/O calls inside `ViewModels/`. `ArchitectureGuardTests` enforces the boundary automatically. |
| 2 | Logic is testable without GTA V | Controller and service tests use `FakeArchive`, `ThrowingStream`, or a temp directory. `dotnet test` passes on a machine with no GTA V install. |
| 3 | Happy-path and failure-path tests exist | At minimum: one test where the feature succeeds end-to-end, one where it fails mid-way and leaves no partial state. |
| 4 | Risky file operations follow the safety sequence | `PathSafety.GetSafePath()` → create dir → copy stream → add to rollback list. Never add to rollback before the write completes. See [installer-safety.md](installer-safety.md). |
| 5 | Failures are logged with feature context | Every catch block calls `AppLogger.Error("[FeatureName] operation failed", ex)`. No silent swallow. |
| 6 | Failures are visible in the UI | A failure that only appears in `app.log` is a bug. Show a banner, toast, or per-item error state. |
| 7 | The feature has a clear module | Lives under `Features/<FeatureName>/` with module, controller interface, and workflow controller. |
| 8 | Regression fixtures exist | Common failure modes (mid-stream crash, duplicate staging, conflict on enable/disable) have a test or a documented "out of scope" reason. |
| 9 | Build and tests pass | `dotnet build LSPDFRManager.sln` → zero errors. `dotnet test` → zero failures. |
| 10 | UI smoke run recorded (if UI changed) | PR description includes `UI smoke run: done by <name> on <yyyy-mm-dd>` or `not run: no WPF UI available`. See [ui-smoke-pr-check.md](ui-smoke-pr-check.md). |

---

## Feature Structure

```
Features/<FeatureName>/
  <FeatureName>FeatureModule.cs        # wires controller + ViewModel
  I<FeatureName>Controller.cs          # one async method per user intent
  <FeatureName>WorkflowController.cs   # orchestrates services
  Commands/
    <Verb><Object>Command.cs           # one IAppCommand per explicit user action
  Models/
    <FeatureName>Result.cs

LSPDFRManager.Tests/
  Features/<FeatureName>/
    <FeatureName>ControllerTests.cs    # happy path + failure path
    <FeatureName>ArchitectureTests.cs  # boundary guard (only if the boundary can regress)
```

Scaffold: `.\tools\New-FeatureSlice.ps1 -Name <FeatureName>`

---

## Logging Conventions

Use `AppLogger` for all structured output. Include the feature name and operation so failures can be triaged from `app.log` without reading source.

```csharp
// Info — operation start/complete
AppLogger.Info("[UpdateCheck] Checking for new release");
AppLogger.Info($"[UpdateCheck] Found v{release.Version}");

// Warning — degraded but not failed
AppLogger.Warning("[UpdateCheck] Rate-limited; skipping until next session");

// Error — always include the exception
AppLogger.Error("[UpdateCheck] Failed to fetch release info", ex);
```

`AppLogger.Entries` is an in-memory list available at runtime. The log file is written to `%APPDATA%\LSPDFRManager\app.log` (one line per entry, with timestamp, version, session ID, and level).

---

## Triage Without Reading the Whole App

When a bug report comes in, start here before touching source:

1. **Read `app.log`** — each line has `[FeatureName] operation` context. Locate the first `[Error]` in the relevant session (session ID is the 8-char hex in every line).
2. **Check `library.json`** — if the issue is install/enable/disable state, this is ground truth. Cross-reference with physical files on disk.
3. **Run `dotnet test --filter "<FeatureName>"`** — reproduces the scenario in isolation. If no matching test exists, that is the gap to fill.
4. **Check `ArchitectureGuardTests`** — if a guard is failing in CI but not locally, a ViewModel is directly calling install infrastructure somewhere.
5. **Check `DiagnosticsOrchestrator`** — for runtime health issues (missing files, bad paths, version drift), diagnostics often surface the root cause before a crash.

---

## Rules (non-negotiable)

- **ViewModels own bindable state and command delegation only.** Orchestration belongs in a controller.
- **Side effects behind explicit commands.** Installs, writes, downloads, and deletes must not start from passive events (property setters, `Loaded`, navigation).
- **External library types stay behind adapters.** `SharpCompress` types must not appear outside `Services/` archive adapters. No leakage into controllers or ViewModels.
- **Event subscriptions have cleanup.** Any object that subscribes to a singleton event (`AppLogger.EntryAdded`, `InstallQueue.*`) must implement `IDisposable` and detach every handler it attaches.
- **Failures are visible.** A failure only in `app.log` is a bug. Always surface it in the UI.

---

## Promoting Feature-Local Code to `Services/`

Only when a second unrelated feature needs it. Premature promotion adds coupling with no consumer and makes the next slice harder to isolate.

---

## Related Docs

- [architecture.md](architecture.md) — layer structure, MVVM patterns, key singletons
- [feature-slice-template.md](feature-slice-template.md) — copy/paste skeleton and guard test strategy
- [installer-safety.md](installer-safety.md) — extraction safety contract and phase gates
- [common-tasks.md](common-tasks.md) — recipes for common changes
- [ui-smoke-pr-check.md](ui-smoke-pr-check.md) — UI smoke checklist
