# Frontend Scaffold Result — Milestone 3

**Date:** 2026-05-19  
**Branch:** main  
**Result:** Complete — all verification commands pass.

---

## 1. Framework and Tool Versions

| Tool | Version |
|------|---------|
| Node.js | v24.14.1 |
| npm | 11.12.1 |
| Vite | 8.0.13 |
| React | 19.2.6 |
| TypeScript | ~6.0.2 |
| react-router-dom | 7.15.1 |
| @tanstack/react-query | 5.100.11 |
| tailwindcss | 4.3.0 |
| @tailwindcss/vite | 4.3.0 (Tailwind v4 Vite plugin) |
| @radix-ui/react-tabs | 1.1.13 |
| @radix-ui/react-dialog | 1.1.15 |
| @radix-ui/react-dropdown-menu | 2.1.16 |

---

## 2. Packages Added

### Runtime dependencies

| Package | Purpose |
|---------|---------|
| `react`, `react-dom` | UI library |
| `react-router-dom` | Client-side routing (all 17 routes) |
| `@tanstack/react-query` | Server state management (wired up, no real calls yet) |
| `tailwindcss`, `@tailwindcss/vite` | Utility CSS (Tailwind v4 via Vite plugin) |
| `@radix-ui/react-tabs` | Accessible tab primitive |
| `@radix-ui/react-dialog` | Accessible dialog primitive |
| `@radix-ui/react-dropdown-menu` | Accessible dropdown primitive |

### Dev dependencies (added by Vite template)

`typescript`, `@types/react`, `@types/react-dom`, `@vitejs/plugin-react`, `eslint`,
`eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `typescript-eslint`, `globals`

---

## 3. Directory Structure Created

```
frontend/
├── package.json
├── tsconfig.json
├── tsconfig.app.json        ← strict: true added
├── tsconfig.node.json
├── vite.config.ts           ← tailwindcss() plugin added
├── dist/                    ← production build output
└── src/
    ├── index.css            ← Tailwind @import + dark theme CSS vars
    ├── main.tsx             ← entry: StrictMode + QueryClientProvider
    ├── App.tsx              ← createBrowserRouter, 17 routes
    ├── routes/
    │   └── routeConfig.ts   ← typed RouteConfig[] with all 17 entries
    ├── pages/               ← 17 stub page components
    │   ├── DashboardPage.tsx
    │   ├── InstallPage.tsx
    │   ├── LibraryPage.tsx
    │   ├── BrowsePage.tsx
    │   ├── BackupsPage.tsx
    │   ├── ConfigPage.tsx
    │   ├── DiagnosticsPage.tsx
    │   ├── HistoryPage.tsx
    │   ├── ProfilesPage.tsx
    │   ├── SettingsPage.tsx
    │   ├── LogsPage.tsx
    │   ├── SafeModePage.tsx
    │   ├── DevDiagnosticsPage.tsx
    │   ├── OivPage.tsx
    │   ├── CleanupPage.tsx
    │   ├── PatrolReadinessPage.tsx
    │   └── SetupWizardPage.tsx
    ├── components/
    │   ├── layout/
    │   │   ├── AppLayout.tsx    ← Sidebar + Outlet
    │   │   └── Sidebar.tsx      ← NavLink-based nav for 16 routes (excludes /setup)
    │   └── ui/
    │       └── StubPage.tsx     ← shared stub component (label, path, sourceView, "Not migrated yet")
    └── lib/
        ├── queryClient.ts       ← QueryClient singleton (staleTime: 10s, retry: 1)
        └── api/
            └── client.ts        ← typed fetch wrapper (get/post/put/delete); no real calls yet
```

---

## 4. Route List Created

| Route | Label | WPF Source View |
|-------|-------|----------------|
| `/` | Dashboard | DashboardView |
| `/install` | Install | InstallView |
| `/library` | Library | LibraryView |
| `/browse` | Browse | BrowseView |
| `/backups` | Backups | BackupsView |
| `/config` | Config | ConfigView |
| `/diagnostics` | Diagnostics | DiagnosticsView |
| `/history` | History | HistoryView |
| `/profiles` | Profiles | ProfilesView |
| `/settings` | Settings | SettingsView |
| `/logs` | Log Viewer | LogViewerView |
| `/safe-mode` | Safe Mode | SafeModeView |
| `/dev-diagnostics` | Dev Diagnostics | DevDiagnosticsView |
| `/oiv` | OIV Creator | OivView |
| `/cleanup` | Cleanup | CleanupView |
| `/patrol-readiness` | Patrol Readiness | PatrolReadinessDashboardView |
| `/setup` | Setup Wizard | SetupWizardView |

---

## 5. Stub Screens Created

All 17 stub pages render identically via `StubPage`:
- Screen label (heading)
- Route path (`/route`)
- WPF source view name
- "Not migrated yet" badge

`/setup` is excluded from the sidebar nav (wizard flow, not a regular nav item).

---

## 6. Scripts Added to package.json

| Script | Command |
|--------|---------|
| `dev` | `vite` |
| `build` | `tsc -b && vite build` |
| `typecheck` | `tsc -b` |
| `lint` | `eslint .` |
| `preview` | `vite preview` |

---

## 7. Commands Run and Results

| Command | Result |
|---------|--------|
| `npm install` (in `frontend/`) | Pass — 214 packages, 0 vulnerabilities |
| `npm run typecheck` | Pass — 0 TypeScript errors |
| `npm run lint` | Pass — 0 lint errors |
| `npm run build` | Pass — dist/ produced (315 kB JS, 6.6 kB CSS) |
| `dotnet build LSPDFRManager.sln` | Pass — 0 errors, 11 pre-existing warnings |
| `dotnet test` | Pass — 914/914 |

---

## 8. Deviations from Migration Plan

| Item | Plan | Actual | Reason |
|------|------|--------|--------|
| Tailwind setup | `tailwindcss` CSS plugin | `@tailwindcss/vite` Vite plugin | Tailwind v4 uses Vite plugin instead of PostCSS; compatible and simpler |
| `ApiError` constructor | Parameter properties | Explicit field + assignment | TypeScript `erasableSyntaxOnly: true` disallows constructor parameter properties |
| `/setup` in nav | Not specified | Excluded from sidebar | Setup wizard is a first-launch flow, not a regular navigation destination |

---

## 9. Remaining Frontend Setup Work

- [ ] Vite proxy config for LocalApi (needed in Milestone 4)
- [ ] Per-feature API client modules in `lib/api/` (one per feature area, Milestones 5–13)
- [ ] TypeScript DTO types in `src/types/` matching C# DTOs (Milestones 5+)
- [ ] React DevTools / TanStack Query DevTools (optional, add in dev mode)
- [ ] 404 / not-found route
- [ ] Error boundary at app level
- [ ] `frontend/.gitignore` already present from Vite scaffold (covers `dist/`, `node_modules/`)

---

## 10. How This Prepares the App for the React UI Migration

- **All 17 routes** are registered and navigable immediately; any stub can be replaced with a real screen without changing routing or layout.
- **`routeConfig.ts`** provides a typed map from route path → WPF source → migration status; future milestones update `status` field from `'stub'` to `'in-progress'` or `'complete'`.
- **`queryClient.ts`** is wired at the app root; any page can call `useQuery` or `useMutation` without additional setup.
- **`lib/api/client.ts`** provides typed `get/post/put/delete` helpers; feature API modules plug in by calling these helpers with `/api/v1/...` paths once LocalApi is live.
- **Tailwind v4** is configured via Vite plugin; no `tailwind.config.js` needed; CSS custom properties define the navy/blue color scheme matching the WPF dark theme.
- **`dist/`** is ready to be embedded in `LSPDFRManager.LocalApi/wwwroot/` and served as static files in Milestone 4.
