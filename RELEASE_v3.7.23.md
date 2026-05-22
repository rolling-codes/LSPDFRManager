# Release v3.7.23 - Bug Fix Marathon

This release consolidates a large round of static-analysis-driven bug fixes across the backend API layer, the React frontend, and the install workflow. No new features — every change is a correctness or reliability fix.

## Critical Fixes

### Install workflow now routes through the full install queue
Previously the `/api/v1/install` endpoint called `FileInstaller` directly, bypassing the install queue entirely. This meant installs triggered from the UI skipped pre-install backups, transaction tracking, and library registration. The endpoint now delegates to `ExecuteInstallCallback` which routes through `InstallQueue` — the same path the WPF install tab uses. Running in standalone/dev mode returns an explicit error instead of silently half-installing.

### Safe-mode restore no longer re-enables unrelated plugins
The safe-mode apply endpoint now writes a `safe_mode_state.json` manifest recording only the files it actually renamed to `.disabled`. The restore endpoint reads that manifest and re-enables only those exact paths. Previously it re-enabled every `.disabled` file in the GTA directory, including files the user had manually disabled before entering safe mode.

## API Fixes

### Cleanup endpoint surfaces delete failures
When `CleanupApplyService` succeeded overall but failed to delete individual files, the `error` field in the response was always `null`. It now synthesizes a message listing the failed paths (e.g. `"Failed to delete 2 item(s): Plugins/mod.asi…"`) when `AbortReason` is null but `FailedPaths` is non-empty.

### Profile DELETE clears ActiveProfileId
Deleting the currently active profile left `AppConfig.ActiveProfileId` pointing at a non-existent profile. The DELETE endpoint now clears and saves `ActiveProfileId` when the deleted ID matches.

### Profile PUT rejects ID mismatch in body
The PUT endpoint now validates that the profile ID in the URL matches the `Id` in the deserialized profile JSON, rejecting mismatches with `400 Bad Request`.

### Browse API URL validation tightened
`PUT /api/v1/config` now validates `BrowseApiBaseUrl` more strictly: must be `http://`, loopback host only (`localhost` or `127.0.0.1`), explicit port, no path, no query, no fragment. Normalizes to `scheme://host:port` before persisting.

### GtaPathValid included in config responses
`GET /api/v1/config` and `PUT /api/v1/config` now return `gtaPathValid: bool` — the result of checking that the path exists and contains a GTA5 executable. The setup redirect in the React shell uses this to avoid redirecting when the path is set but temporarily unmounted.

### Missing endpoints registered
`LocalApiHost` and the standalone `Program.cs` were missing registrations for `MapProfiles`, `MapInstall`, `MapCleanup`, `MapDiagnostics`, and `MapSafeMode`. All five are now registered.

### 204 No Content responses parsed correctly
The API client now returns `undefined` for `204 No Content` responses (and zero-length bodies) instead of attempting to parse an empty string as JSON, which caused delete/disable operations to throw in the frontend.

## Frontend Fixes

### Backup progress bar no longer disappears mid-job
After adding staged progress strings to the backup endpoint (`"Validating paths"`, `"Creating backup…"`, etc.), the `isRunning` check in `BackupsPage` was only matching the literal strings `"Running"` and `"Pending"`. Progress bars vanished the moment the first staged message arrived. Detection is now terminal-state-based: a job is running unless its state is `Completed`, `Failed`, or `Cancelled`.

### Backup list invalidated after job and when path changes
- The `queryClient.invalidateQueries` after a backup job completes is now inside a `useEffect` instead of running in the render body (which caused an infinite re-render loop in React strict mode).
- Changing the backup folder path in Settings now invalidates the `['backups']` query so the list reflects the new folder immediately.

### Library notes input no longer shows stale value
The notes `<textarea>` in the mod library row held its own local state initialised once at mount. After a background query refresh returned updated notes, the input continued showing the pre-refresh value. A `useEffect` now syncs the local state whenever `mod.id` or `mod.notes` changes.

## UI Redesign (v3.7.22)

- **Dashboard** — launch readiness hero card with LSPDFR component status, quick-action links, improved component cards with icon + state badge.
- **Library** — disabled/issues count badges in header, smart empty state with contextual guidance, Clear Filters button, issue-row highlight, `useEffect`-synced notes input.
- **Settings** — section icons, toggle descriptions, NaN-safe numeric inputs, sticky save footer with unsaved-changes indicator.

## Quality

- Version bumped to `3.7.23`.
- Build: 0 errors.
- Tests: 1012/1012 passing.
- TypeScript: 0 errors.
- Frontend production build passes.

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.21...v3.7.23
