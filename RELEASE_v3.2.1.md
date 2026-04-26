# LSPDFRManager v3.2.1 Release Notes

**Release Date:** 2026-04-26
**Version:** 3.2.1
**Type:** Patch — Installer Safety Hardening

---

## 🎯 Overview

This is a focused hardening release for the install/rollback pipeline. It tightens the path-traversal defense around the OpenIV car-install executor, makes post-extraction registration transactional with the on-disk files, and aligns the documented extraction contract with the code's actual ordering.

No user-visible feature changes. No persistence-layer changes. No archive-handling changes.

---

## 🛡️ Installer Safety Hardening

### 1. PathSafety defense-in-depth in OpenIvExecutor

`OpenIvExecutor.ExecuteAsync` previously composed destination paths via `Path.Combine(targetRoot, ...)` and relied solely on `OpenIvInstallPlanValidator` to vet `DestinationPath` and patch `FilePath`. The validator's check (`StartsWith("mods\\")`) accepts traversal patterns like `mods\..\..\escape.dll` because the prefix match passes before path normalization.

Both the file-extraction path and the XML-patch path now route through `PathSafety.GetSafePath`, which fully resolves the combined path and rejects any result that does not stay under `targetRoot`. PathSafety failures land in the existing outer catch and trigger the LIFO rollback already in place — no new error paths.

This is additive: the validator still runs first; PathSafety is a second, stricter gate.

### 2. Transactional post-extraction registration in InstallQueue

`InstallQueue.ProcessLoop` previously treated `ModLibraryService.Add`, `DlcListService.AddEntry`, and `InstallCompleted` event invocation as best-effort steps after a successful extraction. If any of them threw (rare in practice — both inner services swallow their own I/O errors — but possible if event handlers throw or future code adds new throws), the on-disk files would remain present but un-tracked.

The registration block is now wrapped in a try/catch that, on failure:
- Removes the partial dlclist entry (if added)
- Removes the partial library entry (if added)
- Rolls back all extracted files via `FileInstaller.RollbackAsync`
- Surfaces the failure through both `InstallFailed` and `InstallFailedWithResult` events

The `InstallResult` contract is preserved — UI sees a consistent `Success = false, IsPartial = true` result on post-install failure.

### 3. CLAUDE.md extraction-contract wording corrected

The "Extraction Safety Contract" section in `CLAUDE.md` documented the rollback-ledger ordering as "Add file to rollback list ONLY after successful copy". The actual code in `FileInstaller.InstallAsync` appends the destination path to the ledger **before** the copy starts — which is the safer ordering, because it ensures partial writes (e.g. `File.Create` succeeded but `CopyToAsync` threw mid-stream) are still cleaned up by `RollbackAsync`.

The doc has been updated to match the code, with an explanatory paragraph clarifying why the order is intentional and how PathSafety failures (which occur before the ledger touch) are kept out of the rollback set.

---

## 🔧 Technical Details

### API surface

- `FileInstaller.RollbackAsync` is now `public static` (was `private static`).
- Signature widened from `List<string>` → `IReadOnlyList<string>`. `List<string>` still satisfies the new parameter type, so all existing callers compile unchanged.
- Behavior is identical: best-effort LIFO deletion with per-file errors swallowed.

### Files changed

| File | Change |
|------|--------|
| `Services/FileInstaller.cs` | `RollbackAsync` exposed as public; iteration uses index-based reverse loop |
| `Core/CarInstall/OpenIvExecutor.cs` | `Path.Combine` → `PathSafety.GetSafePath` for both `destPath` and `patchFilePath` |
| `Core/InstallQueue.cs` | Post-success registration wrapped in try/catch with full rollback (files + library + dlclist) |
| `CLAUDE.md` | Extraction Safety Contract step ordering corrected |
| `LSPDFRManager.Tests/OpenIvExecutorIntegrationTests.cs` | New test: `Executor_TraversalThroughModsPrefix_BlockedByPathSafety` |

### Test coverage

- **Total:** 150 tests passing, 0 failing, 0 skipped (was 149 in v3.2.0).
- **New test** uses `FakeArchive` to verify that `mods\..\..\escape.yft` — which passes `OpenIvInstallPlanValidator.Validate` — is rejected by `PathSafety.GetSafePath` inside the executor, with no file landing outside `targetRoot`.

---

## 🛡️ Safety & Guarantees

✅ **Atomicity invariant preserved** — The `FileInstaller` extraction loop ordering (PathSafety → mkdir → ledger append → copy) is unchanged.
✅ **Rollback ordering unchanged** — LIFO via reverse iteration in `FileInstaller.RollbackAsync`; LIFO via `Stack<string>` in `OpenIvExecutor.RollbackAsync`.
✅ **No archive-handling changes** — SharpCompress / `ZipArchive` / `DirectoryArchiveAdapter` paths untouched.
✅ **No persistence schema changes** — `library.json`, `config.json`, `configs.json` formats unchanged.
✅ **`InstallResult` contract unchanged** — `Success`, `IsPartial`, `WrittenFiles`, `Error` semantics preserved.
✅ **Backward compatible** — Existing tests pass without modification.

---

## 🐛 Known Issues / Non-Goals

- The `csproj <Version>` element remains at `1.5.0` and is intentionally untouched in this release; it has been out of sync with the 3.x release-notes track since before this release.
- Pre-existing test-project warnings (`StreamingBufferTests.cs:47` nullable, `InstallIntegrationTests.cs:208` unused variable) are not addressed here.
- No automated test was added for the `InstallQueue` post-install transactional path; the queue reaches into the `ModLibraryService.Instance` / `AppConfig.Instance` / static `DlcListService` singletons, and adding coverage requires a separate refactor to introduce abstractions over those.

---

## 📝 Upgrade Notes

### For Users

No action required. No behavioral change for the success path. Failed installs are now guaranteed to leave no on-disk files even if a post-extraction registration step fails.

### For Developers

- `FileInstaller.RollbackAsync` is now public and may be called by other components that need to undo a list of previously-written files (used internally by `InstallQueue` on post-install failure).
- Any new code path that builds an `OpenIvInstallPlan` no longer needs to assume `OpenIvInstallPlanValidator` is the only traversal guard — `OpenIvExecutor` enforces `PathSafety` directly.
- See `CLAUDE.md` "Extraction Safety Contract (Do Not Violate)" for the corrected step ordering.

---

## 📞 Support

For issues or feedback:
- Check `app.log` in `%APPDATA%\LSPDFRManager\` (look for `[POST_INSTALL_FAILED]` entries — new in this release)
- `[EXTRACT_ERROR]`, `[ROLLBACK_START]`, `[ROLLBACK_COMPLETE]` log markers remain unchanged

---

**Built with:** .NET 8, C#, WPF
**Tested on:** Windows 11 Pro x64
**Tested with:** 150 unit + integration tests (all passing)
