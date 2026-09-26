# Release v3.7.24 — Reliability & Hardening

## Highlights

A focused reliability release from a full multi-agent code review. No new
features — this hardens the install/rollback path, closes silent data-loss
windows in persistence, removes a concurrency race in the job queue, and stops
the local API from leaking internal file paths in error responses.

## Install integrity

- **OIV executor no longer truncates files in place.** `OpenIvExecutor` now
  writes each extracted file to a temp sibling and commits it with an atomic
  `File.Move`. Previously an overwrite was truncated before the new bytes
  landed, so a crash or power-loss mid-copy could leave a valid game file
  destroyed. The original is now intact until the move succeeds.
- **Faster, safer archive lookups.** The executor builds a one-time key→entry
  map instead of re-enumerating the archive per operation (O(n²) → O(n)), which
  also avoids re-opening entries out of order on non-seekable archive streams.

## Data-loss fixes

- **Change history** is now persisted through the atomic, logged `JsonFileStore`
  instead of a bare `catch {}` that silently discarded the audit trail on any
  I/O error. Entry-list access is guarded by a lock.
- **Profiles** no longer vanish silently: a corrupt profile file is logged and
  skipped, and stock defaults are never seeded over profiles that merely failed
  to parse (which previously replaced the user's real profiles). Profiles are
  written atomically.
- **Restore points** access is now lock-guarded with snapshot reads, closing a
  torn-read race between the UI and background saves.

## Security & concurrency

- **Job queue race fixed.** `/jobs/{id}` pollers can no longer observe torn job
  state (e.g. `State=Completed` before the result is visible); all job-state
  access is serialized and readers receive a detached snapshot.
- **Safe-mode restore is contained.** The restore endpoint now verifies each
  path in the safe-mode manifest resolves inside the configured GTA V root
  before moving it, so a tampered or corrupt manifest cannot move files to or
  from arbitrary locations.
- **API errors no longer leak paths.** The local API's 500-level handlers route
  through a new `ApiErrors.Problem` helper that logs the full exception
  server-side with a correlation id and returns only a generic message —
  absolute `%APPDATA%`/GTA paths are no longer exposed to the client.

## Verification

- 1021/1021 tests passing (8 new tests: overwrite-failure integrity, path
  containment).
- Clean build.
