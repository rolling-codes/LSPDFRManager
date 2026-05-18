# Release v3.7.17 - Cleanup Tab Crash Fix

## Bug Fixes

### Cleanup tab crash on open (v3.7.16 regression)
- Fixed crash when navigating to the Cleanup tab. A `Run.Text` binding to the read-only `ConfirmPhrase` property defaulted to `TwoWay` mode, causing WPF to throw an unhandled exception during layout.

### Cleanup confirmation UX simplified
- Removed typed confirmation phrase requirement from all cleanup modes. The confirmation screen no longer requires typing a phrase before the Delete button is enabled.
- Confirm button is now enabled as soon as at least one item is selected, matching standard destructive-action UX patterns.
- All other safety measures remain: preview before delete, backup before delete, abort on backup failure, explicit Confirm button click required.

## Quality

- Build: 0 errors.
- Tests: 914/914 passing (includes 32 navigation smoke tests and cleanup regression tests).
