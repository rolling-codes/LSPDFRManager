# Release v3.7.18 - React Preview and Shared Core Extraction

## Highlights

### Shared core library extraction
- Moved shared domain models, services, command contracts, feature contracts, and car-install planning code into `LSPDFRManager.Shared`.
- Updated the WPF app and tests to reference the shared project directly, keeping the existing desktop workflows available while making the core logic reusable.

### Local API host
- Added `LSPDFRManager.LocalApi`, an in-process local API host for desktop-backed frontend integration.
- Added localhost-only middleware, job queue support, DTOs, and endpoints for the existing app areas including backups, browse, cleanup, config, diagnostics, history, install, library, logs, patrol readiness, profiles, and safe mode.

### React UI preview
- Added the React frontend scaffold and bundled static web assets.
- Added a WPF React UI preview navigation entry and view model so the desktop app can host the new preview path.

## Quality

- Version bumped to `3.7.18`.
- Frontend production build completed successfully.
- Build: Release build completed with 0 errors.
- Tests: 914/914 passing in Debug and Release.

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.17...v3.7.18
