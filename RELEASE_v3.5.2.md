# LSPDFR Manager v3.5.2

This patch release focuses on safer installs, less data loss, and a more useful Library/Profile workflow.

## Stability and Safety

- Library/config/settings JSON writes now use atomic temp-file replacement to reduce corruption risk during crashes or concurrent saves.
- Mod notes now autosave with a short debounce instead of saving on every keystroke, preventing lost notes during refresh/filter/navigation while reducing save churn.
- Startup validation now checks AppData writability, GTA V folder existence, `GTA5.exe`, and minimum free disk space for both AppData and the GTA V drive.
- Install failures surface through the existing global UI error banner and the Install tab error panel.
- Install registration and library mutations are serialized so install completion, uninstall, enable/disable, and save operations cannot race the library file.
- Conflict detection now includes disabled `.disabled` files on disk, including cases where a disabled file exists outside the current library records.
- Pre-install backups now run when `AppConfig.AutoBackupOnInstall` is enabled.
- Install logs are trimmed using `AppConfig.MaxInstallLogEntries`.

## Library and Profiles

- Profiles created from the Profiles tab now snapshot the current enabled/disabled setup instead of creating an empty shell.
- Profile apply now correctly handles enabled files and `.disabled` files, updates library state afterward, and preserves load-order priority metadata.
- Added persistent load-order priority to installed mods.
- Added Library sorting by load order plus Up/Down controls for selected mods.
- Added Undo for bulk enable/disable operations.
- Added enabled-mod export to Markdown/Text.
- Added persistent Library search history storage.
- Disabled mods remain visually distinct with opacity/status/badge treatment and the existing disabled count.

## Install UX

- Duplicate mod detection now prompts before install:
  - Yes: replace existing duplicate entries.
  - No: install as a separate entry.
  - Cancel: skip.
- Archive detection progress text is clearer while the manager reads and scans dropped archives.
- File-conflict prompts include conflicts against disabled installed mods and `.disabled` files.

## Config Editor

- JSON, XML, and `.meta` edits are validated before save.
- Source config writes use rollback backups and atomic temp writes where possible.
- If a source config save fails, the previous file is restored and the UI shows the failure.

## Documentation

- README updated for the current patch release.
- Added troubleshooting coverage for install failures, conflicts, GTA crashes, missing mods, and Browse API setup.

## Validation

- `dotnet restore` passed.
- `dotnet build --configuration Release` passed.
- `dotnet test --configuration Release --no-build` passed: 223 tests.
- `dotnet publish LSPDFRManager.csproj --configuration Release --no-build` passed.

## Download

- `LSPDFRManager-v3.5.2-win-x64.zip`
