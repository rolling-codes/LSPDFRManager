# Release v3.7.19 - Full TypeScript Stack and UI Polish

## Highlights

### Full TypeScript frontend stack
- The React preview has moved to a full TypeScript stack for the frontend workflow.
- Route metadata, shared UI primitives, page components, API types, and migrated feature surfaces are now typed end-to-end.
- The production TypeScript build is part of the release gate and refreshes the packaged `wwwroot` assets.

### More professional React UI
- Reworked the React shell with a polished desktop-tool layout, grouped navigation, top status bar, and responsive mobile route selector.
- Added shared `Page`, `Panel`, `StateMessage`, and `StatusBadge` primitives for consistent page structure and state handling.
- Polished Dashboard, Library, Install, Logs, History, Settings, Cleanup, Patrol Readiness, and migration-placeholder pages.
- Added Lucide icons for navigation, status, actions, toggles, and workflow feedback.

### Cleanup
- Removed unused Vite starter styles and sample assets from the frontend.
- Rebuilt bundled LocalApi static assets for the updated React UI.

## Quality

- Version bumped to `3.7.19`.
- Frontend lint passes.
- Frontend production build passes.
- Build: Release build completed with 0 errors.
- Tests: 914/914 passing in Debug and Release.

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.18...v3.7.19
