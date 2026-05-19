# Baseline Verification — React Migration

**Date/time:** 2026-05-19  
**Branch:** main  
**HEAD commit:** `53ee06f` — fix(logging): write errors to logs/errors.log in app directory  
**Version:** v3.7.17

---

## Working Tree Status

Clean. Only untracked files present (no modified tracked files):

```
Untracked files:
  REACT_MIGRATION_ANALYSIS.md   ← migration docs, not part of app
  REACT_MIGRATION_PLAN.md       ← migration docs, not part of app
  "e unexpected files are present."  ← pre-existing untracked artifact
```

No staged changes. No modified source files. Safe to begin migration work.

---

## Commands Run

### 1. `dotnet restore LSPDFRManager.sln`

**Result: PASS**

```
All projects are up-to-date for restore.
```

Warnings (pre-existing, non-blocking):
- `NU1902: Package 'SharpCompress' 0.38.0 has a known moderate severity vulnerability`  
  This advisory was present before this session began and is documented in project memory as a known non-blocker.

---

### 2. `dotnet build LSPDFRManager.sln --configuration Release --no-restore`

**Result: PASS — 0 errors**

```
Build succeeded.
    6 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.36
```

Outputs:
- `LSPDFRManager.Api` → `bin\Release\net8.0\LSPDFRManager.Api.dll`
- `LSPDFRManager` → `bin\Release\net8.0-windows\LSPDFRManager.dll`
- `LSPDFRManager.Tests` → `LSPDFRManager.Tests\bin\Release\net8.0-windows\LSPDFRManager.Tests.dll`

Warnings (all pre-existing):

| Warning | File | Notes |
|---------|------|-------|
| `NU1902` SharpCompress vulnerability | Both projects | Known advisory; non-blocker |
| `CS0219` variable assigned but never used (`failureRaised`) | `InstallIntegrationTests.cs:221` | Pre-existing test warning |
| `CS8602` dereference of possibly null | `StreamingBufferTests.cs:47` | Pre-existing test warning |
| `CS8605` unboxing possibly null | `StreamingBufferTests.cs:47` | Pre-existing test warning |
| `xUnit2029` use DoesNotContain instead of Empty | `EupBackupEditorTests.cs:439` | Pre-existing xUnit analyzer suggestion |

---

### 3. `dotnet test LSPDFRManager.Tests\LSPDFRManager.Tests.csproj --configuration Release --no-build`

**Result: PASS — 914/914 tests passed**

```
Total tests: 914
     Passed: 914
      Failed: 0
     Skipped: 0
Total time: 10.7260 Seconds
    0 Error(s)
```

> Note: Previous recorded baseline was 878 tests (v3.7.16 / v3.7.17). The count is now 914 — additional tests were added in commits since the last recorded snapshot. All 914 pass.

---

## Summary of Warnings / Failures

| Item | Type | Severity | Action |
|------|------|----------|--------|
| `NU1902` SharpCompress advisory | Dependency warning | Pre-existing, non-blocking | No action needed |
| `CS0219` unused variable | Code warning | Pre-existing | No action needed |
| `CS8602` / `CS8605` null warnings | Code warning | Pre-existing | No action needed |
| `xUnit2029` | Analyzer suggestion | Pre-existing | No action needed |

No new warnings introduced. No errors. No failing tests.

---

## Safe to Proceed to Milestone 1?

**Yes.** The repository is in a fully clean, passing state.

- 0 build errors
- 914/914 tests passing
- No modified tracked source files
- Pre-existing warnings are known and non-blocking

---

## Recommended Next Step

Begin **Milestone 1**: Scaffold `LSPDFRManager.LocalApi` — a new ASP.NET Core Minimal API project that will serve as the local management API for the React frontend.

Per `REACT_MIGRATION_PLAN.md` Milestone 1:
1. Create `LSPDFRManager.LocalApi/` as a new `Microsoft.NET.Sdk.Web` project targeting `net8.0`.
2. Add minimal `Program.cs` with a `/health` endpoint.
3. Add `LocalhostOnlyMiddleware` (reject non-`127.0.0.1` Host headers).
4. Add to `LSPDFRManager.sln`.
5. Verify `dotnet build` — 0 errors.
6. Verify `dotnet test` — all 914 tests still pass.

> **Important prerequisite for Milestone 2 (shared library extraction):** `Domain/`, `Services/`, and `Core/` currently live inside `LSPDFRManager.csproj` which targets `net8.0-windows`. For `LSPDFRManager.LocalApi` (targeting `net8.0`) to reference these, they must be extracted to a new `LSPDFRManager.Core` class library project. This is the highest-risk structural change and should be planned carefully before execution.
