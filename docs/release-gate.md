# Release Gate

Full validation checklist for every release. The release is not done until the packaged EXE artifact passes all sections below. Run this against the Release build, not Debug.

---

## Pre-release cleanup

Before tagging:

- Confirm release notes are internally consistent
- Confirm version numbers say the target version everywhere
- Confirm `AGENTS.md` and `CLAUDE.md` match
- Confirm focus files reference the current smart-feature platform
- Confirm old issue references are closed or moved to future milestones
- Confirm Patrol Readiness is enabled by default if stable
- Confirm experimental features remain behind flags
- Verify the test count in release notes matches the actual final run

Do not publish conflicting test counts. Use the actual output of `dotnet test`.

---

## Test gate

```powershell
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
dotnet test --filter "ArchitectureGuard"
dotnet test --filter "CommandCenter"
dotnet test --filter "PatrolReadiness"
```

All must pass before packaging.

---

## Build the release artifact

```powershell
dotnet clean
dotnet restore
dotnet build LSPDFRManager.sln -c Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj -c Release
```

Then package using the documented command in CLAUDE.md (update version number):

```powershell
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/vX.Y.Z -p:DebugType=None -p:DebugSymbols=false
```

---

## Manual verification checklist

Run against the packaged EXE, not `dotnet run`.

### Startup

- [ ] App launches from Release build
- [ ] Main window opens cleanly
- [ ] Sidebar loads
- [ ] No startup crash
- [ ] No missing resource errors
- [ ] No broken DataTemplates

### Navigation

- [ ] Patrol Ready nav item appears
- [ ] Patrol Ready opens correct dashboard
- [ ] Diagnostics opens correctly
- [ ] Existing pages still open correctly
- [ ] Active nav state updates correctly

### Button press / command wiring

For every new or changed button, verify the actual click path in the packaged EXE. Do not mark this complete just because the button is visible or the ViewModel command exists.

- [ ] Click each new or changed button once with valid inputs
- [ ] Click each new or changed button once with missing/invalid inputs when applicable
- [ ] Confirm the bound command runs exactly once per click
- [ ] Confirm disabled buttons cannot be invoked by mouse or keyboard
- [ ] Confirm button state changes after the action when expected: enabled, disabled, busy, success, or error
- [ ] Confirm the user-visible result appears: navigation, dialog, toast/status text, file, support bundle, scan result, or log entry
- [ ] Confirm no duplicate dialogs, duplicate queue entries, duplicate logs, or duplicate file operations occur
- [ ] Confirm Enter/Space keyboard activation works for focused primary buttons where expected
- [ ] Confirm buttons still work after navigating away and back
- [ ] Confirm exceptions from button actions are surfaced as friendly UI errors and logged

### Patrol Readiness

Test with at least: a valid install, a broken install, and a partial/dirty install.

- [ ] Status badge shows green / amber / red correctly
- [ ] Score is 0–100
- [ ] Blocking issues appear
- [ ] Warnings appear
- [ ] Info items appear
- [ ] Suggested fixes display
- [ ] Known-good diff banner appears when relevant
- [ ] No raw exceptions appear in UI
- [ ] Scan can be repeated
- [ ] Scan Now button executes the scan and updates score/issues
- [ ] Mark as Known-Good button writes the baseline and updates the dashboard
- [ ] Export Support Bundle button creates a ZIP and reports success/failure

### Diagnostics

- [ ] Feature flags load
- [ ] Feature flags toggle / reset correctly
- [ ] Log tail loads
- [ ] Support bundle export button works
- [ ] Diagnostics page does not crash without logs
- [ ] Toggle buttons change persisted feature flag state exactly once per click
- [ ] Reset buttons restore default feature flag state exactly once per click

### Support Bundle

Export a bundle and inspect the ZIP contents:

**Must include:**
- `app-info.json`
- `feature-flags.json`
- installed mods list
- diagnostic log tail
- RPH logs if present
- sanitized paths

**Must not include:**
- Unsanitized full user profile paths
- Secrets or credentials
- Large unintended archives
- Unrelated personal files

### Known-good baseline

- [ ] Mark Known-Good works
- [ ] Diff Current vs Known-Good works
- [ ] Dashboard reflects known-good changes
- [ ] Missing baseline does not crash

---

## Artifact verification

After packaging, test the generated EXE or ZIP:

- [ ] EXE launches
- [ ] No missing DLL or resource errors
- [ ] Patrol Ready page opens
- [ ] Diagnostics page opens
- [ ] Support Bundle export works
- [ ] Button press / command wiring checklist passes for changed UI
- [ ] `%APPDATA%\LSPDFRManager\feature-flags.json` is created or read correctly
- [ ] No silent file operations occur

---

## GitHub release checklist

- [ ] Create version tag (e.g. `v3.8.0`)
- [ ] Create GitHub release
- [ ] Paste final release notes
- [ ] Attach EXE / ZIP artifact
- [ ] Attach checksum if available
- [ ] Mark milestone complete
- [ ] Confirm all release issues are closed
- [ ] Move unfinished items to future milestones

---

## Post-release checks

After uploading:

- [ ] Download the GitHub artifact yourself
- [ ] Run the downloaded EXE
- [ ] Confirm version shown in app matches the tag
- [ ] Confirm release notes link points to the correct release
- [ ] Confirm no debug-only files are included
- [ ] Open clean issues for any deferred features

---

## Release rule

The release is ready only when the Release EXE launches cleanly, changed buttons are clicked and verified, Patrol Readiness works, Diagnostics works, Support Bundle export works, all tests pass, and the GitHub release contains the verified artifact.

If there is a problem — do not stack changes. Fix only the release blocker, rebuild, retest, and ship.
