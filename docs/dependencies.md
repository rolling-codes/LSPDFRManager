# Dependency Notes

## SharpCompress 0.38.0

SharpCompress is used for RAR and 7-Zip archive reading behind the `IArchive` / `IArchiveEntry` adapter boundary. ZIP handling uses `System.IO.Compression`.

Current triage:

- NuGet reports a moderate SharpCompress advisory for the pinned package.
- The relevant risky API family is bulk extraction such as `WriteToDirectory`.
- LSPDFRManager does not use `WriteToDirectory`; archive entries are streamed through adapters.
- Installer writes still go through the safety sequence documented in [installer-safety.md](installer-safety.md): `PathSafety.GetSafePath()` first, create directory, copy stream, then add rollback entry only after success.
- Do not bump SharpCompress inside unrelated feature/controller PRs. Bump in a dedicated dependency PR with archive adapter tests and real RAR/7z smoke coverage.

Decision: defer the bump until a dedicated dependency pass. The current installer path is mitigated by adapter boundaries and explicit path safety checks.
