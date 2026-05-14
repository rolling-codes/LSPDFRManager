# Manual UI Smoke Test — v3.7.4

Run this checklist before each release. Automated UI automation is not yet implemented; this checklist is the required substitute.

## Prerequisites

- Built or published exe available
- No real GTA V path configured (or point to a non-existent path to test missing-path handling)
- No GTA V running

---

## 1. App Launch

| Step | Expected | Pass? |
|------|----------|-------|
| Launch `LSPDFRManager.exe` | App starts within 5 seconds | |
| Startup warning dialog appears (if GTA path missing) | Dialog shows path warning, does NOT crash | |
| Dismiss dialog (OK) | Main window appears with sidebar and content area | |
| Sidebar tabs visible | Dashboard, Library, Install, Browse, Diagnostics, Settings | |

---

## 2. Settings — GTA Path

| Step | Expected | Pass? |
|------|----------|-------|
| Navigate to Settings tab | Settings panel renders without exception | |
| Observe GTA path field | Shows configured path or empty | |
| Enter a non-existent path | No crash; validation message shown or field accepts it | |

---

## 3. Diagnostics — Setup Doctor with missing GTA path

| Step | Expected | Pass? |
|------|----------|-------|
| Navigate to Diagnostics tab | Diagnostics panel renders | |
| Click "Run Diagnostics" (or equivalent) | Scan begins; progress indicator appears | |
| Scan completes | Findings list populated | |
| Findings include "GTA path" error | Severity = Error, Category = Setup | |
| No crash during scan | App remains responsive | |

---

## 4. Diagnostics — With valid GTA-like temp folder

| Step | Expected | Pass? |
|------|----------|-------|
| Set GTA path to any real writable folder (e.g. a temp dir) | Settings accepts it | |
| Run Diagnostics again | Scan completes | |
| Recipe validator findings appear (missing files) | At least one finding from recipe/dependency category | |
| Keybind findings (if INI files present) | Conflicts shown or no conflicts shown — no crash | |
| Backup Easy Editor findings | Shown if UltimateBackup folder found, or absent if not | |

---

## 5. HTML Report Export

| Step | Expected | Pass? |
|------|----------|-------|
| After running diagnostics, export as HTML | File created at chosen path | |
| Open exported HTML in browser | Valid HTML, no broken tags, no raw `<` or `>` in content cells | |
| Finding titles with special characters render correctly | HTML-encoded, not injected as raw markup | |

---

## 6. Preview-before-Apply Safety

| Step | Expected | Pass? |
|------|----------|-------|
| If Preset Patch panel is visible: open any preset | Preview shown without writing files | |
| If Backup Easy Editor panel is visible: open a mapping preview | Preview shown without writing files | |
| Cancel/close preview | No files modified on disk | |

---

## 7. App Close

| Step | Expected | Pass? |
|------|----------|-------|
| Close main window (X button) | App exits cleanly, exit code 0 | |
| No crash dialogs on exit | Clean shutdown | |

---

## Automated Verification Status

Launch verification (Debug + Release publish): **automated** — runs as part of Phase 10 gate via PowerShell process check.

UI flow verification: **manual checklist** — automated WPF UI testing (FlaUI/UIAutomation) not yet implemented. This checklist must be executed manually before each release.

Automating the Diagnostics smoke path is a recommended future improvement.
