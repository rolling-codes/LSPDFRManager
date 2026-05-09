# Troubleshooting

## Install Failures

1. Open Settings and confirm the GTA V path points to the folder containing `GTA5.exe`.
2. Make sure the AppData folder is writable. LSPDFR Manager stores config, library, profile, restore-point, and history data there.
3. Check available disk space on both the AppData drive and GTA V install drive. The minimum warning threshold is controlled by `AppConfig.MinimumFreeDiskSpaceMb`.
4. Review the Install tab log. Failed installs now show a visible error and keep the newest entries up to `AppConfig.MaxInstallLogEntries`.
5. If a partial extraction fails, the installer attempts to roll back written files and restore overwritten files from its temporary rollback backup.

## File Conflicts and Duplicate Mods

- Before install, the manager checks incoming files against enabled mods, disabled mods, and physical `.disabled` files.
- If a duplicate mod name is detected:
  - Choose **Yes** to replace the existing copy.
  - Choose **No** to install a separate copy.
  - Choose **Cancel** to skip the install.
- If conflicts mention disabled files, check whether an old `*.disabled` file should be restored, deleted, or kept as part of a profile.

## GTA V Crashes After Enabling Mods

1. Use Profiles to switch back to a known-good setup.
2. Sort the Library by load order and move high-risk plugins later or disable them temporarily.
3. Run Diagnostics and check for missing dependencies, duplicate plugins, disabled files, or common plugin health issues.
4. Use the Config tab to validate edited JSON/XML/meta configs. Invalid edits are blocked before saving.
5. Restore the latest restore point or backup if a profile switch or config edit introduced the crash.

## Missing Mods

- Disabled mods are renamed with a `.disabled` suffix. They remain counted and visually marked in the Library.
- If a mod appears missing after a profile switch, check whether the active profile disabled it.
- If files exist on disk but not in the Library, reinstall the mod or restore the latest app-data backup so the library record and files are back in sync.

## Browse API Setup

The Browse API is optional. If you use it:

1. Build or locate the `LSPDFRManager.Api` executable.
2. In Settings, set the Browse API executable path.
3. Set the base URL, usually `http://localhost:5284`.
4. Enable auto-start only if you want the desktop app to launch the API for you.
5. If Browse fails, confirm the executable exists, the port is not already in use, and the configured URL is a valid absolute URL.
