# LSPDFR Manager — React Migration Plan

**Generated:** 2026-05-19  
**Analysis basis:** `REACT_MIGRATION_ANALYSIS.md`

---

## 1. Recommended Architecture

**Option selected: React frontend hosted inside a WebView2-based WPF shell, communicating with a new local ASP.NET Core management API.**

```
┌─────────────────────────────────────────────────────────┐
│  LSPDFRManager.exe  (WPF shell)                         │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │  WebView2 control                                 │  │
│  │  → http://localhost:<port>  (React SPA)           │  │
│  └───────────────────────────────────────────────────┘  │
│                         │ HTTP (localhost only)          │
│  ┌───────────────────────▼───────────────────────────┐  │
│  │  LSPDFRManager.LocalApi  (ASP.NET Core Minimal)   │  │
│  │  Started in-process by WPF shell                  │  │
│  │  Listens on 127.0.0.1 only                        │  │
│  │  Calls existing C# Services / Core / Domain       │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  Existing C# Services / Core / Domain (unchanged)       │
│  Existing %APPDATA%\LSPDFRManager\ persistence          │
└─────────────────────────────────────────────────────────┘

Separate process (existing):
  LSPDFRManager.Api  (scraper for lcpdfr.com — unchanged)
```

The React SPA is served as **static files** from the WPF binary. The WPF shell hosts a WebView2 control pointed at `http://127.0.0.1:<port>/`. The ASP.NET Core local API is embedded in the same process or started as a sibling process.

---

## 2. Why This Architecture Fits This Repo

- **Preserves the entire C# service layer.** FileInstaller, PathSafety, rollback, backups, profiles, diagnostics — none of these need to be touched or ported. Only a thin API shim is added on top.
- **Preserves packaging.** Still ships as a `.exe` + DLLs ZIP. No Electron runtime.
- **Preserves Windows integration.** WebView2 is already in the dependency list (`Microsoft.Web.WebView2`). The Browse tab already uses it.
- **Preserves safety invariants.** PathSafety, rollback, and backup-before-delete all run server-side. The React layer never touches the filesystem.
- **Preserves tests.** Existing 878 xUnit tests run unchanged against the same C# services.
- **Incremental migration.** WPF views can be replaced one at a time while the shell remains functional.
- **Localhost-only API.** Bound to `127.0.0.1`; not reachable from the network.

---

## 3. Why Rejected Alternatives Were Not Chosen

### Rejected: Browser-only React SPA (no desktop shell)

- Cannot open native file dialogs — browser `<input type=file>` does not return full paths on Windows.
- Cannot write to `%APPDATA%` or the GTA directory.
- Cannot run SharpCompress for archive extraction.
- Cannot enforce `PathSafety.GetSafePath()` for incoming paths (client-side validation is insufficient).
- Cannot host WebView2 for the Browse tab.
- **Eliminated by analysis section 11.**

### Rejected: Electron shell

- Adds ~120–200 MB Chromium + Node.js runtime to the release ZIP.
- Current users expect a small self-contained `.exe`. Electron changes the packaging contract significantly.
- Requires Node.js IPC instead of direct C# service calls.
- Provides no meaningful advantage over WebView2 + WPF shell for this use case.
- WebView2 is already present in the dependency tree.

### Rejected: Tauri shell (Rust backend)

- Would require rewriting all C# services in Rust.
- All existing tests would be invalidated.
- Incompatible with the "preserve proven C# filesystem logic" invariant.

### Rejected: MAUI Hybrid

- MAUI Blazor Hybrid uses a Blazor/Razor component model, not React + TypeScript.
- Smaller ecosystem; harder to hire for; does not satisfy the React + TypeScript requirement.

---

## 4. Proposed Target Directory Structure

```
LSPDFRManager-3.7.4/
│
├── LSPDFRManager.csproj          ← WPF shell (keeps WebView2, shrinks XAML)
├── MainWindow.xaml               ← Becomes a thin shell (just WebView2)
│
├── LSPDFRManager.LocalApi/       ← NEW: local management API
│   ├── LSPDFRManager.LocalApi.csproj
│   ├── Program.cs                ← ASP.NET Core Minimal API startup
│   ├── Controllers/              ← Endpoint groups (one per feature area)
│   ├── Dtos/                     ← Request/response DTOs (flat records)
│   └── Middleware/               ← Error handling, localhost-only guard
│
├── LSPDFRManager.Api/            ← Existing scraper API (unchanged)
│
├── LSPDFRManager.Tests/          ← Existing tests (unchanged)
│   └── Api/                      ← NEW: API-layer integration tests
│
├── frontend/                     ← NEW: React + TypeScript SPA
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   ├── src/
│   │   ├── main.tsx
│   │   ├── App.tsx
│   │   ├── router.tsx
│   │   ├── api/                  ← API client modules (one per feature)
│   │   ├── components/           ← Shared UI components
│   │   ├── pages/                ← One folder per route/view
│   │   │   ├── dashboard/
│   │   │   ├── install/
│   │   │   ├── library/
│   │   │   ├── browse/
│   │   │   ├── backups/
│   │   │   ├── config/
│   │   │   ├── diagnostics/
│   │   │   ├── history/
│   │   │   ├── profiles/
│   │   │   ├── settings/
│   │   │   ├── logs/
│   │   │   ├── safe-mode/
│   │   │   ├── oiv/
│   │   │   ├── cleanup/
│   │   │   ├── patrol-readiness/
│   │   │   └── setup-wizard/
│   │   └── types/                ← TypeScript types matching C# DTOs
│   └── dist/                     ← Built SPA (served by LocalApi)
│
├── Domain/                       ← Unchanged
├── Services/                     ← Unchanged
├── Core/                         ← Unchanged
└── ...
```

---

## 5. C# Projects: Keep, Split, Create, or Retire

| Project | Action | Rationale |
|---------|--------|-----------|
| `LSPDFRManager.csproj` (WPF) | **Keep, shrink** | Becomes a thin WebView2 shell. XAML views removed one at a time as React routes reach parity. |
| `LSPDFRManager.Api` (scraper) | **Keep unchanged** | Already working; Browse route will continue calling it. |
| `LSPDFRManager.Tests` | **Keep, expand** | All existing tests stay. New API-layer tests added. |
| `LSPDFRManager.LocalApi` | **Create new** | Exposes C# services as local HTTP API for React. |
| Domain/, Services/, Core/ | **Keep unchanged** | These are the business logic. No structural changes in Phase 1. |

---

## 6. React App Setup Recommendation

- **Bundler:** Vite (fast dev server; production build produces static files for embedding)
- **Language:** TypeScript (strict mode)
- **Router:** React Router v7 (file-based or config-based; 17 routes)
- **State:** React Query (TanStack Query) for server state; `useState`/`useReducer` for local UI state
- **UI components:** Radix UI primitives + Tailwind CSS (accessible, unstyled primitives + utility classes)
- **Testing:** Vitest + React Testing Library

**No**: Redux, MobX, GraphQL, Next.js (SSR not needed), or heavy component frameworks.

Frontend dev server: `npm run dev` proxies API calls to `http://localhost:<localApiPort>`.

Production: `npm run build` → `dist/` → embedded as static files in the LocalApi project and served at `/`.

---

## 7. API Boundary Design

### General conventions

- All endpoints under `/api/v1/`
- JSON request and response bodies
- Standard HTTP status codes (200, 400, 404, 409, 500)
- Long-running operations: POST to start → returns a `jobId` → GET `/api/v1/jobs/{jobId}/status` for progress
- Error response shape: `{ "error": "...", "details": "..." }`

### Endpoint groups

| Group | Prefix | Key endpoints |
|-------|--------|--------------|
| Config | `/api/v1/config` | GET, PUT config; POST validate-gta-path |
| Mods (library) | `/api/v1/mods` | GET installed, POST enable, POST disable, DELETE |
| Install | `/api/v1/install` | POST start, GET job status |
| Backups | `/api/v1/backups` | GET list, POST create, POST restore, DELETE |
| Profiles | `/api/v1/profiles` | GET list, POST create, PUT switch, DELETE |
| Config files | `/api/v1/config-files` | GET discovered, GET preview, POST apply |
| Diagnostics | `/api/v1/diagnostics` | POST run, GET report |
| History | `/api/v1/history` | GET entries |
| Logs | `/api/v1/logs` | GET (paginated) |
| Safe mode | `/api/v1/safe-mode` | GET plan, POST apply, POST restore |
| OIV | `/api/v1/oiv` | POST inspect, POST create, POST install |
| Cleanup | `/api/v1/cleanup` | GET scan, POST apply |
| Patrol readiness | `/api/v1/patrol-readiness` | GET result |
| Compatibility | `/api/v1/compatibility` | GET bundle |
| Setup wizard | `/api/v1/setup` | POST scan, POST complete |
| Jobs | `/api/v1/jobs/{id}` | GET status, DELETE (cancel) |

---

## 8. DTOs / Contracts Between React and Backend

Each DTO is a flat C# record in `LSPDFRManager.LocalApi/Dtos/`. TypeScript equivalents live in `frontend/src/types/`.

Key DTOs to define early:

```csharp
// Config
record AppConfigDto(string GtaPath, bool AutoBackup, ...);

// Library
record InstalledModDto(string Id, string Name, string Type, bool IsEnabled, string InstalledAt);
record ToggleModRequest(string ModId, bool Enabled);

// Install
record StartInstallRequest(string SourcePath, string? SubfolderHint);
record InstallJobStatusDto(string JobId, string State, int ProgressPct, string? Error, InstallResultDto? Result);

// Backup
record BackupDto(string Id, string CreatedAt, long SizeBytes, string? Label);
record CreateBackupRequest(string? Label);

// Jobs (long-running ops)
record JobStatusDto(string JobId, string State, int ProgressPct, string? Error);
```

All paths that originate from the React layer (e.g., `SourcePath` in install) must be re-validated server-side with `PathSafety.GetSafePath()` before any file operation.

---

## 9. Localhost / API Security Considerations

- `LSPDFRManager.LocalApi` must bind to `127.0.0.1` only — never `0.0.0.0`.
- Add middleware that rejects any request whose `Host` header is not `localhost` or `127.0.0.1`.
- Port is chosen randomly at startup or from config; communicated to the WPF shell via process argument or named pipe.
- No authentication required (process-local; only the local user's session can reach it), but requests from the browser must include a session token (random UUID generated at startup) in a custom header to prevent DNS rebinding attacks.
- All incoming path strings must pass `PathSafety.GetSafePath()` before any filesystem operation.
- Input validation on all endpoints: reject null/empty required fields, validate path existence where applicable.

---

## 10. Long-Running Operation and Cancellation Strategy

Operations that take >1 second (install, backup, cleanup, diagnostics, profile switch) use a **job queue pattern**:

1. React POSTs to start the operation → receives `{ jobId: "..." }`.
2. React polls `GET /api/v1/jobs/{jobId}/status` every 500ms for `{ state, progressPct, error, result }`.
3. React can DELETE `jobs/{jobId}` to request cancellation (sets a `CancellationToken`).
4. The C# service receives `CancellationToken` and propagates it through async calls.
5. On cancellation: rollback is triggered (same as failure path in FileInstaller).

A `JobQueue` service in `LocalApi` holds a `ConcurrentDictionary<string, JobEntry>` with state + token source. Jobs older than 10 minutes are pruned.

**Alternative considered:** Server-Sent Events (SSE) for real-time progress. Simpler for the client but adds complexity to the API. Polling at 500ms is acceptable given operation durations. SSE can be added later.

---

## 11. Error Handling Strategy

### API layer

- All endpoints wrapped in try/catch returning `{ error, details }` on 500.
- Domain exceptions (`InvalidOperationException` from PathSafety, `InstallResult.Success == false`) mapped to 400/409.
- Validation failures return 400 with field-level detail.

### React layer

- Every API call goes through a typed API client module in `frontend/src/api/`.
- Each client function returns `{ data, error }` (never throws).
- Each page component has: Loading state, Error state (with retry), Empty state, Success state.
- Errors surfaced in a toast notification + inline in the affected form area.
- Long-running job failures must display the backend's `InstallResult.UserMessage` verbatim (it is already user-friendly).

---

## 12. Styling Approach

- **Tailwind CSS** — utility classes, zero runtime overhead, consistent spacing/color system.
- Color palette derives from the existing navy/blue theme (`Resources/Colors.xaml`). Extract hex values and define as Tailwind theme tokens.
- Radix UI primitives for accessible components (Dialog, DropdownMenu, Tabs, etc.).
- No CSS-in-JS; no component library with opinionated styling (avoids generic AI-app aesthetic).
- Dark mode: default dark (matches current WPF dark theme).

---

## 13. Testing Strategy

### Existing (keep unchanged)

- All 878 xUnit tests continue to run against C# services directly.

### New C# API-layer tests

- Integration tests in `LSPDFRManager.Tests/Api/` using `WebApplicationFactory<Program>` from `LSPDFRManager.LocalApi`.
- Cover: input validation, PathSafety enforcement, error response shape, job lifecycle.
- Must be added **before** wiring dangerous operations (install, delete, cleanup, safe mode) to the React UI.

### New frontend tests

- Unit tests: Vitest + React Testing Library for components with non-trivial logic.
- API client mocking: `msw` (Mock Service Worker) for component-level tests.
- No E2E tests in Phase 1 (add later if needed).

---

## 14. Manual QA Checklist

Before marking any milestone complete:

- [ ] `dotnet build LSPDFRManager.sln` — 0 errors
- [ ] `dotnet test` — all existing tests pass (no regressions)
- [ ] `npm run typecheck` — 0 TypeScript errors
- [ ] `npm run lint` — 0 lint errors
- [ ] `npm run build` — clean production build
- [ ] App starts; WebView2 loads React frontend
- [ ] Navigation to all implemented routes works
- [ ] Each implemented screen: loading, empty, error, and success states render correctly
- [ ] Install: path traversal attempt rejected server-side
- [ ] Install: rollback verified on forced failure
- [ ] Backup: backup created before any destructive operation
- [ ] Cleanup: backup ZIP created before deletion
- [ ] Settings: GTA path validation rejects non-existent paths
- [ ] All screens: no console errors in DevTools
- [ ] Localhost binding: API not reachable from another machine

---

## 15. Step-by-Step Implementation Milestones

### Milestone 0 — Verify baseline (no changes to application code)

1. `dotnet restore LSPDFRManager.sln`
2. `dotnet build LSPDFRManager.sln`
3. `dotnet test` — confirm 878 tests pass
4. Record baseline.

### Milestone 1 — Scaffold LocalApi project

1. Create `LSPDFRManager.LocalApi/` as a new `Microsoft.NET.Sdk.Web` project targeting `net8.0`.
2. Add minimal `Program.cs` with health endpoint at `/health`.
3. Add `LocalhostOnlyMiddleware` (reject non-127.0.0.1 host headers).
4. Add project reference to main app services (or extract a shared library — see note below).
5. Add to `LSPDFRManager.sln`.
6. `dotnet build` — 0 errors.
7. `dotnet test` — all existing tests pass.

> **Note on sharing services:** `Services/`, `Domain/`, `Core/` currently live in the WPF project (`net8.0-windows`). To reference them from `net8.0` (cross-platform) LocalApi, they must be extracted to a `LSPDFRManager.Core.csproj` targeting `net8.0` (no WPF APIs). This is the biggest structural change in Phase 1 and must be done carefully.

### Milestone 2 — Extract shared library

1. Create `LSPDFRManager.Core.csproj` targeting `net8.0` (not `net8.0-windows`).
2. Move: `Domain/`, `Services/`, `Core/` files that do not reference WPF APIs.
3. Keep WPF-dependent code (ViewModels, Views, Converters, `UiDispatcher`, `InstallQueue`) in the main WPF project.
4. Add project reference from WPF project → `LSPDFRManager.Core`.
5. Add project reference from `LSPDFRManager.LocalApi` → `LSPDFRManager.Core`.
6. Update `LSPDFRManager.Tests` references.
7. `dotnet build` — 0 errors.
8. `dotnet test` — all existing tests pass.

### Milestone 3 — Scaffold React frontend

1. Create `frontend/` with Vite + React + TypeScript.
2. Install: `react`, `react-dom`, `react-router-dom`, `@tanstack/react-query`, `tailwindcss`, `@radix-ui/react-*`.
3. Add skeleton `App.tsx` with all 17 routes defined (stub pages showing route name only).
4. `npm run typecheck` — 0 errors.
5. `npm run lint` — 0 errors.
6. `npm run build` — clean build producing `dist/`.

### Milestone 4 — Wire LocalApi to serve React static files

1. Add `app.UseStaticFiles()` + `app.MapFallbackToFile("index.html")` in LocalApi.
2. Copy `frontend/dist/` into LocalApi's wwwroot as part of the build pipeline.
3. Start LocalApi from WPF shell on app startup.
4. Open WebView2 pointing at `http://127.0.0.1:{port}/`.
5. Verify React skeleton loads in the embedded browser.

### Milestone 5 — Read-only screens (Dashboard, History, Logs, Diagnostics)

For each screen:
1. Add C# endpoint(s) in LocalApi.
2. Add TypeScript DTO types.
3. Add API client module.
4. Implement React page (loading / error / success states).
5. Add API-layer integration tests for the new endpoint(s).
6. Manual smoke: navigate to route, see real data.

### Milestone 6 — Settings + Config (read + write, non-destructive)

1. GET/PUT config endpoint.
2. POST validate-gta-path endpoint.
3. Settings page with form + save.
4. API-layer tests for validation.

### Milestone 7 — Library (enable/disable — low-blast-radius write ops)

1. GET installed mods, POST enable, POST disable.
2. Library page with search, enable/disable toggles.
3. API-layer tests.

### Milestone 8 — Profiles (create, switch, delete)

1. CRUD profile endpoints.
2. Profile switch endpoint (calls `ProfileManager`).
3. Profiles page.
4. API-layer tests.

### Milestone 9 — Backups (create, restore, delete)

1. Backup CRUD endpoints.
2. Long-running job pattern for create/restore.
3. Backups page with progress indicator.
4. API-layer tests including backup-before-delete invariant.

### Milestone 10 — Install (highest risk)

1. POST start-install endpoint with job queue.
2. GET job status endpoint.
3. DELETE job (cancel) endpoint with `CancellationToken` propagation.
4. Input validation: source path validated with `PathSafety` server-side.
5. Native file-dialog workaround: either use WebView2 postMessage to trigger WPF `OpenFileDialog`, or add a dedicated `/api/v1/dialogs/open-file` endpoint.
6. Install page: conflict preview, progress bar, result display.
7. API-layer integration tests: path traversal rejection, rollback on forced failure.
8. Manual QA: all MANUAL_TEST_SCENARIOS.md scenarios.

### Milestone 11 — Cleanup, Safe Mode (high-blast-radius)

1. Cleanup scan + apply endpoints (backup-before-delete enforced server-side).
2. Safe mode plan + apply endpoints.
3. Cleanup and SafeMode pages with confirmation gates.
4. API-layer tests.

### Milestone 12 — OIV, Patrol Readiness, Setup Wizard

1. OIV inspect/create/install endpoints.
2. Patrol readiness endpoint.
3. Setup wizard flow endpoint.
4. Corresponding pages.

### Milestone 13 — Browse tab

1. Reuse existing `LSPDFRManager.Api` scraper endpoints.
2. Implement a search-results React page calling the existing API.
3. Evaluate whether to keep the full WebView2 Browse tab or replace with the React search UI.

### Milestone 14 — WPF shell cleanup

1. Once all React routes are at parity, remove WPF views one by one.
2. Keep WPF shell (`MainWindow.xaml`) as a thin WebView2 host.
3. Remove WPF ViewModels and Views directories when empty.
4. Do not remove until React route is verified in production build.

### Milestone 15 — Final polish and release packaging

1. Update CI workflow to build React frontend before publish.
2. Update `dotnet publish` to include `frontend/dist/` in the release.
3. Update README and CLAUDE.md.
4. Full manual QA pass.
5. Tag and release.

---

## 16. Rollback Plan

At each milestone:

- The WPF UI remains functional until the React route reaches parity. If a React route is broken, the user can fall back to the WPF view.
- Git branches per milestone; squash-merge to main only after milestone passes manual QA.
- `LSPDFRManager.Core` extraction (Milestone 2) is the riskiest structural change. If it causes instability, revert by keeping services in the WPF project and using `net8.0-windows` for LocalApi (acceptable tradeoff for Windows-only app).
- No JSON persistence format changes in Phase 1. If schema changes are needed in Phase 2+, a documented migration must be written first.

---

## 17. Explicit List of Files Likely to Be Changed

### New files (create)

- `LSPDFRManager.LocalApi/LSPDFRManager.LocalApi.csproj`
- `LSPDFRManager.LocalApi/Program.cs`
- `LSPDFRManager.LocalApi/Middleware/LocalhostOnlyMiddleware.cs`
- `LSPDFRManager.LocalApi/Dtos/*.cs` (one per feature area)
- `LSPDFRManager.LocalApi/Controllers/*.cs` (one per feature area)
- `LSPDFRManager.Core/LSPDFRManager.Core.csproj` (if shared-library extraction done)
- `frontend/package.json`, `tsconfig.json`, `vite.config.ts`
- `frontend/src/**` (all React source files)
- `LSPDFRManager.Tests/Api/*.cs` (API-layer tests)
- `REACT_MIGRATION_ANALYSIS.md` (already created)
- `REACT_MIGRATION_PLAN.md` (this file)

### Modified files

- `LSPDFRManager.sln` — add new projects
- `LSPDFRManager.csproj` — add WebView2 shell code, project references
- `MainWindow.xaml` / `MainWindow.xaml.cs` — transition to thin WebView2 host
- `App.xaml.cs` — start LocalApi process/in-process host
- `.github/workflows/dotnet.yml` — add frontend build steps
- `SOURCE_OVERVIEW.md` — update with new structure
- `README.md` — update dev setup instructions
- `CLAUDE.md` (if it exists) — update architecture notes

### Files that must NOT be changed until parity is reached

- All `Services/` files
- All `Domain/` files
- All `Core/` files
- All `LSPDFRManager.Tests/` files (add to, never delete or weaken)
- All `Views/`, `ViewModels/`, `Converters/` until the React route replaces them

---

## 18. Verification Commands for Each Milestone

```powershell
# Every milestone
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests\LSPDFRManager.Tests.csproj --configuration Release

# Milestone 3+ (frontend)
cd frontend
npm install
npm run typecheck
npm run lint
npm run build

# Milestone 4+ (integration)
# Start LocalApi; verify WebView2 loads React; check DevTools console for errors

# Milestone 10 (install safety)
# Run all MANUAL_TEST_SCENARIOS.md scenarios against the packaged binary
```

All commands must pass with zero errors before a milestone is considered complete.
