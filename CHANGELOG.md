# Changelog

All notable changes to this project are documented here. This project follows
[Semantic Versioning](https://semver.org/). Per-release detail lives in the
`RELEASE_v*.md` files; this file is the running summary.

## [3.7.24] — 2026-09-26

### Fixed
- **Install integrity:** `OpenIvExecutor` writes to a temp file and commits with
  an atomic move, so a failed/interrupted overwrite never truncates an existing
  game file in place.
- **Change history:** persisted via atomic `JsonFileStore` and lock-guarded;
  saves are no longer silently swallowed on I/O error.
- **Profiles:** corrupt profile files are logged and skipped instead of dropped,
  stock defaults are never seeded over unparseable user data, and writes are
  atomic.
- **Restore points:** lock-guarded snapshot reads close a torn-read race.
- **Job queue:** all job-state access is serialized and readers get a snapshot,
  removing a race where `/jobs/{id}` could return torn state.

### Security
- **Safe-mode restore** verifies each manifest path resolves inside the GTA V
  root before moving it, containing tampered/corrupt manifests.
- **Local API** error responses route through `ApiErrors.Problem`, logging the
  exception server-side with a correlation id instead of leaking absolute file
  paths to the client.

### Performance
- `OpenIvExecutor` builds a one-time archive entry lookup (O(n²) → O(n)).

### Tests
- 1021 passing (+8): overwrite-failure integrity, path containment.
