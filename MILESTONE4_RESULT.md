# Milestone 4 Result — Wire LocalApi to Serve React Frontend

**Date:** 2026-05-19  
**Branch:** main  
**Result:** Complete — 0 build errors, 914/914 tests pass.

---

## 1. What Was Done

### Frontend build output redirected to LocalApi/wwwroot
`frontend/vite.config.ts` updated: `build.outDir = '../LSPDFRManager.LocalApi/wwwroot'`  
Running `npm run build` now writes `index.html` + assets directly into the LocalApi project.

### LocalApi — static file serving added
`Program.cs` updated with `UseDefaultFiles()` + `UseStaticFiles()` + `MapFallbackToFile("index.html")`.  
React SPA routing works: any unknown path falls back to `index.html`.

### LocalApiHost — in-process host
New `LSPDFRManager.LocalApi/LocalApiHost.cs`:
- Picks a free TCP port at startup via `TcpListener` on port 0
- Exposes `PortTask` (a `Task<int>`) that completes once the server is listening
- Exposes `BaseUrl` (`http://127.0.0.1:{port}`)
- `StartAsync()` configures and starts a `WebApplication` in-process (non-blocking)
- `StopAsync()` shuts it down cleanly

### LocalApi.csproj — wwwroot copied to output
```xml
<Content Update="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
```
Uses `Update` (not `Include`) to avoid duplicate-item error with the Web SDK's implicit Content glob.  
wwwroot files are copied to the WPF output directory so `AppContext.BaseDirectory + "wwwroot"` resolves correctly at runtime.

### WPF project — references LocalApi
`LSPDFRManager.csproj`:
- Added `<ProjectReference Include="LSPDFRManager.LocalApi\LSPDFRManager.LocalApi.csproj" />`
- Added `frontend\**` to all four SDK Remove patterns (Compile, EmbeddedResource, None, Page) to prevent VS from treating `frontend/` source as WPF project content.

### WPF shell — LocalApiHost started on startup
`App.xaml.cs`:
- `OnStartup`: fires `LocalApiHost.StartAsync()` on a background thread immediately (non-blocking)
- `OnExit`: calls `LocalApiHost.StopAsync()`

### New WPF view — ReactPreviewView
`ViewModels/ReactPreviewViewModel.cs`:
- Exposes `IsReady` and `StatusText`
- `WaitForReadyAsync()` awaits `LocalApiHost.PortTask` then signals ready

`Views/ReactPreviewView.xaml` + `ReactPreviewView.xaml.cs`:
- Shows "Starting local API…" until ready
- Initializes WebView2 with a dedicated profile (`%APPDATA%/LSPDFRManager/WebView2React`)
- Navigates to `LocalApiHost.BaseUrl` once the port is ready

### MainViewModel + App.xaml + MainWindow.xaml — nav wired
- `MainViewModel`: added `ReactPreviewVM`, `IsReactPreviewActive`, `"ReactPreview"` case in Navigate switch
- `App.xaml`: added `DataTemplate` for `ReactPreviewViewModel → ReactPreviewView`
- `MainWindow.xaml`: added "React UI" nav button (Segoe MDL2 icon &#xE774;, nav key `ReactPreview`)

---

## 2. IDE-Only Warnings (Not Build Errors)

After adding the LocalApi project reference, the Roslyn IDE analyzer shows type-conflict hints in `App.xaml.cs` for `AppDataPaths`, `LspdfrInstallLocator`, etc.:

> "The type 'AppDataPaths' in '...\LSPDFRManager.Shared\Services\AppDataPaths.cs' conflicts with the imported type 'AppDataPaths' in 'LSPDFRManager.Shared, Version=3.7.17.0, Culture=neutral, PublicKeyToken=null'"

**Root cause:** The IDE's Roslyn analyzer sees the type both via the direct `ProjectReference → Shared` (which includes source compilation) and via `LocalApi → Shared` (DLL import). MSBuild deduplicates at compile time; `dotnet build` produces 0 warnings and 0 errors.

**Impact:** None — compiler agrees, all 914 tests pass. Resolvable in a future milestone by restructuring the project graph (e.g., adding `ExcludeAssets="compile"` to the direct Shared reference).

---

## 3. Runtime Behavior

On app startup:
1. WPF shell starts normally (existing UI unchanged)
2. `LocalApiHost.StartAsync()` runs in the background — picks a free port, starts ASP.NET Core
3. Navigating to "React UI" in the sidebar shows a loading message
4. Once `PortTask` completes, WebView2 initializes and navigates to `http://127.0.0.1:{port}/`
5. The React SPA loads with all 17 route stubs and the sidebar nav

All existing WPF views remain fully functional.

---

## 4. Commands Run and Results

| Command | Result |
|---------|--------|
| `npm run build` (frontend/) | Pass — dist/ written to `LSPDFRManager.LocalApi/wwwroot/` |
| `npm run typecheck` | Pass — 0 errors |
| `npm run lint` | Pass — 0 errors |
| `dotnet build LSPDFRManager.sln` | Pass — 0 errors |
| `dotnet test` | Pass — 914/914 |

---

## 5. Files Created or Modified

### New files
- `LSPDFRManager.LocalApi/LocalApiHost.cs`
- `ViewModels/ReactPreviewViewModel.cs`
- `Views/ReactPreviewView.xaml`
- `Views/ReactPreviewView.xaml.cs`
- `MILESTONE4_RESULT.md`

### Modified files
- `frontend/vite.config.ts` — build.outDir → LocalApi/wwwroot
- `LSPDFRManager.LocalApi/Program.cs` — static files + fallback
- `LSPDFRManager.LocalApi/LSPDFRManager.LocalApi.csproj` — wwwroot Content Update
- `LSPDFRManager.csproj` — added LocalApi ProjectReference; frontend\** exclusion
- `App.xaml.cs` — LocalApiHost.StartAsync/StopAsync
- `App.xaml` — DataTemplate for ReactPreviewViewModel
- `MainWindow.xaml` — React UI nav button
- `ViewModels/MainViewModel.cs` — ReactPreviewVM, IsReactPreviewActive, nav case

---

## 6. Remaining Work

- [ ] IDE type-conflict hints from double Shared reference — resolvable by adding `<ExcludeAssets>compile</ExcludeAssets>` or restructuring (Milestone 14+)
- [ ] Vite proxy config for dev server (so `npm run dev` proxies `/api` to LocalApi during frontend development)
- [ ] Port communicated to WPF shell — currently the WebView2 uses `LocalApiHost.BaseUrl` (static). Could be passed as a window title or IPC for more explicit WPF/React integration
- [ ] Session token for DNS rebinding protection (Milestone 9 security concern from migration plan)
- [ ] API endpoint groups (Milestones 5–13)
