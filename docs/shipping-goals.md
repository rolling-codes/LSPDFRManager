# Shipping Goals

This document defines what "shipped" means for every release of LSPDFR Manager.
A release is not done until every item in the active checklist is complete.

---

## Definition of Shipped

A version is shipped when:

1. **All tests pass** — `dotnet test` exits 0 with 0 failures.
2. **Build is clean** — `dotnet build` has 0 errors (warnings are documented, not ignored).
3. **Executable is published** — a framework-dependent, single-exe release build is produced and verified to launch.
4. **Release ZIP is clean** — contains only runtime artifacts; no `obj/`, `bin/Debug/`, `*.pdb`, `*.cs`, `*.csproj`, WebView2 cache, or test assemblies.
5. **GitHub release is tagged** — a `vX.Y.Z` tag exists on the release commit, marked as Latest on GitHub.
6. **Release notes are accurate** — `RELEASE_vX.Y.Z.md` matches what was actually shipped.
7. **Smoke run is recorded** — WPF/WebView smoke checklist (see `docs/ui-smoke-pr-check.md`) completed or explicitly deferred with a reason.

---

## What Has Been Shipped

### v3.7.10 — Control Layer, OIV Templates & Updates Slice
**Tag:** `v3.7.10` · **Release:** https://github.com/rolling-codes/LSPDFRManager/releases/tag/v3.7.10

- Control layer foundation: `IAppCommand`, `AsyncAppCommand`, `IFeatureController`, `IFeatureModule`
- Install slice: `IInstallController` routes detect/stage/plan/confirm
- Library slice: `ILibraryController` owns bulk toggle + undo
- OIV Creator Templates (Feature #37): zero side-effect selection, explicit Apply/Undo, PathSafety-validated paths, `IsUserEdited` respected
- Updates slice Phase 1: check-only via `IUpdateController`; no auto-download/apply
- Dialog abstraction (PR4): `IFileDialogService` for deterministic tests
- Architecture guard tests + feature slice tooling (`New-FeatureSlice.ps1`)
- Security hardening: Zip Slip prevention in OivPackageBuilder, segment-based traversal check in OivPackageValidator
- Bug fixes: `AppLogger.Warn` typo, `AsyncAppCommand` UI-thread marshalling, `pathsSkippedUnsafe` counter, RelayCommand cast removal
- **Tests:** 734/734 passing
- **Executable:** framework-dependent win-x64 build (see release assets)
- **Smoke run:** pending (no WPF environment available in build agent)

---

## Release Checklist (copy for each release)

```
### vX.Y.Z — [Title]

- [ ] dotnet build LSPDFRManager.sln — 0 errors
- [ ] dotnet test — 0 failures, all N tests pass
- [ ] dotnet publish (framework-dependent, win-x64) succeeds
- [ ] Executable launches: window renders, no startup crash
- [ ] Release ZIP contents verified (no debug/source/cache artifacts)
- [ ] RELEASE_vX.Y.Z.md written and accurate
- [ ] git tag vX.Y.Z pushed to origin
- [ ] GitHub release created and marked Latest
- [ ] UI smoke run: done by [name] on [date] / deferred: [reason]
```

---

## Build Commands

**Publish executable (framework-dependent):**
```powershell
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o publish/vX.Y.Z
```

**Assemble release ZIP:**
```powershell
$ver = "vX.Y.Z"
New-Item -ItemType Directory -Path "release-package/LSPDFRManager-$ver" -Force
Copy-Item "publish/$ver/*" "release-package/LSPDFRManager-$ver" -Recurse
Copy-Item "RELEASE_$ver.md" "release-package/LSPDFRManager-$ver"
Copy-Item "docs/" "release-package/LSPDFRManager-$ver/docs" -Recurse
Compress-Archive "release-package/LSPDFRManager-$ver" "LSPDFRManager-$ver-win-x64.zip"
```

**Strip check (run before publishing ZIP):**
```powershell
# Should return empty — if it doesn't, something leaked in
Get-ChildItem "release-package/LSPDFRManager-$ver" -Recurse -Include `
  "*.cs","*.csproj","*.sln","*.pdb","*.log","appsettings.Development.json" |
  Select-Object FullName
```
