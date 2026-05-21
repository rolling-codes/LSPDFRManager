# Common Tasks & Gotchas

## Common Tasks

**Add a new mod type**
- Add entry to ModType enum [Domain/ModType.cs](../Domain/ModType.cs)
- Update ModDetector detection logic [Services/ModDetector.cs](../Services/ModDetector.cs)
- Add UI for type filtering in LibraryView/ViewModel (search/filter dropdown)

**Add app setting**
- Add property to AppConfig [Domain/AppConfig.cs](../Domain/AppConfig.cs)
- Update config.json schema
- Wire UI control in SettingsView/SettingsViewModel

**Debug install failures**
- Check app.log for error message
- InstallQueue.ProcessLoop logs InstallStarted/InstallFailed events
- Verify GTA path is valid and writable
- Confirm archive is not corrupted (SharpCompress extraction)

**Profile performance**
- Enable verbose logging in AppLogger.cs (all Service methods log entry/exit)
- Check backup/restore speed (large libraries = slow JSON I/O)
- Archive extraction bottleneck for large mods (SharpCompress perf)

**Add a new diagnostic check**
- Implement check in `SetupDoctorService` or a dedicated scanner service
- Register it in `DiagnosticsOrchestrator`
- Return `DiagnosticFinding` with Category, Title, Detail, Severity, Confidence=1.0

**Add a new install safety rule**
- Add logic to [Services/InstallerSafetyPolicy.cs](../Services/InstallerSafetyPolicy.cs)
- Update `SmartInstallPlanner` if ordering or plan-level logic changes
- Add corresponding test in `SmartInstallPlannerTests` or `FileInstallerSafetyPolicyTests`

**Add a new feature slice**
- Start from [feature-slice-template.md](feature-slice-template.md)
- Create `Features/<FeatureName>/` with module, controller, models, commands, and tests
- Prefer `.\tools\New-FeatureSlice.ps1 -Name <FeatureName>` to create the starting files
- Wire module registration at the current composition point
- Keep ViewModel orchestration as delegation to the controller
- Put side effects behind explicit commands; do not start writes/downloads/installs from passive events
- Add or extend one architecture guard only when the boundary could regress
- Add two tests: controller happy path and one regression tripwire
- Optional scaffold: `.\tools\New-FeatureSlice.ps1 -Name <FeatureName>`

## Gotchas

**Archive Extraction**
- SharpCompress extracts RAR/7-Zip; ZipFile (System.IO.Compression) for ZIP
- Duplicate file paths in archive (rare) — only first is extracted
- No password-protected archive support

**File Locking**
- On enable/disable, renames files on disk while GTA V may have them open
- May fail silently if GTA V holds file handle — check app.log for rename errors

**Testing**
- AppConfig and ModLibraryService are singletons; tests must reset Instance before/after each test
- No mocking of file I/O or ModLibraryService — use real temp directories and JSON serialization
