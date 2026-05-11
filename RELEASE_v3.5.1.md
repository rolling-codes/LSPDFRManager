# LSPDFR Manager v3.5.1 — Patch

**Release Date:** 2026-05-08
**Version:** 3.5.1
**Type:** Bug-fix patch (no new features)

---

## Summary

v3.5.1 addresses five reliability and usability bugs reported after the v3.5.0 Command Center release. No new features are introduced; the fixes are targeted and safe to apply over any existing v3.5.0 installation.

---

## Bug Fixes

### 1 — Installation log grows unbounded (Issue 7)

**Problem:** The install log `ObservableCollection` in the Install tab accumulated entries indefinitely. After 10+ installs the log grew to thousands of lines, causing UI lag and increasing memory usage linearly.

**Fix:**
- Log is now capped at **500 entries**. Oldest entries are removed automatically when the limit is exceeded.
- A **Clear** button has been added to the log header for manual clearing.

---

### 2 — Bulk enable/disable triggered N sequential JSON saves (Issue 9)

**Problem:** "Enable visible" / "Disable visible" called `SetEnabled()` once per mod, each triggering a full `library.json` serialization. Disabling 50 mods caused 50 separate disk writes, blocking the UI for several seconds.

**Fix:**
- New `SetEnabledBatch(IEnumerable<Guid> ids, bool enabled)` method on `ModLibraryService` performs all file rename operations first, then writes `library.json` exactly once.
- Bulk operations on 100+ mods now complete near-instantly with a single save.

---

### 3 — No startup validation — crashes on first run (Issue 5)

**Problem:** If the AppData directory was not writable, or the configured GTA V path did not exist, the app threw an unhandled exception with no user-visible explanation.

**Fix:**
- `App.xaml.cs` now runs `ValidateStartup()` before the main window opens.
- Checks: AppData folder writability (probe file test) and GTA V path existence.
- Failures show a **user-friendly warning dialog** listing the specific problem and remediation steps ("Open Settings to set the correct path").
- The app still opens — the dialog is a warning, not a hard block.

---

### 4 — Disabled mod file conflicts not detected before install (Issue 4)

**Problem:** The pre-install conflict check only warned about file conflicts with *enabled* mods. A disabled mod's files (stored with original paths in `InstalledFiles`) were not surfaced, so installing a conflicting mod could silently overwrite the disabled mod's files on disk — and re-enabling the disabled mod would overwrite the new install.

**Fix:**
- `InstallViewModel.InstallAsync()` now checks incoming file paths against **all installed mods** (enabled and disabled) before queuing the install.
- If conflicts are found, a confirmation dialog lists the conflicting mods and marks disabled ones with `(disabled)`.
- The user can still proceed — the dialog is a confirmation, not a block.

---

### 5 — Disabled mods not visually distinct in Library (Issue 11)

**Problem:** Disabled and enabled mods looked nearly identical. The only visual difference was a reduced opacity (0.6) which was subtle, especially on smaller screens. Users had to hover or read the status badge text to determine a mod's state.

**Fix:**
- A dark **DISABLED** badge now appears on mod cards when a mod is disabled, alongside the existing opacity reduction.
- The badge uses the same pill style as the existing CONFLICT badge for visual consistency.
- `ModItemViewModel` exposes `IsDisabled` (computed from `IsEnabled`) so the badge binding is clean and property-change notifications are correct.

---

## Changed Files

| File | Change |
|:-----|:-------|
| `ViewModels/InstallViewModel.cs` | Log cap (500), `ClearLogCommand`, pre-install conflict check |
| `ViewModels/LibraryViewModel.cs` | Bulk ops use `SetEnabledBatch` |
| `ViewModels/ModItemViewModel.cs` | `IsDisabled` property + notification |
| `Services/ModLibraryService.cs` | `SetEnabledBatch` method |
| `Views/InstallView.xaml` | Clear button in log header |
| `Views/Components/ModCard.xaml` | DISABLED badge |
| `App.xaml.cs` | `ValidateStartup()` with dialog |

---

## Tests

All **220 tests pass** with 0 failures. No new tests were added (the fixes are UI-layer or thin service changes; existing integration tests cover the affected paths).

---

## Upgrade Notes

- Drop-in replacement for v3.5.0. No migration needed.
- `library.json` and `config.json` formats are unchanged.
- The new `ClearLogCommand` binding in `InstallView.xaml` is self-contained — no XAML resource changes required.

---

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Release: https://github.com/rolling-codes/LSPDFRManager/releases/tag/v3.5.1
