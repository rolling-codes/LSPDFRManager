# Release v3.6.1 — Safety and UI Maintenance Patch

v3.6.1 is a maintenance release that hardens the installation pipeline and polishes the user interface.

## Key Improvements

### 1. Atomic Installation Rollback
Previously, if an installation failed midway, any files that were overwritten by the installer were lost upon rollback (the installer simply deleted the destination files). 
v3.6.1 introduces **backup-before-overwrite** logic:
- A temporary backup is created for any file about to be overwritten.
- During rollback, these original files are restored.
- If no original existed, the file is deleted as before.
- This ensures your GTA V installation remains consistent even after a failed mod install.

### 2. WebView2 Stability
- The "Install This Mod" button is now reactive to the browser's state. It remains disabled until WebView2 has successfully initialized.
- Added internal null-checks to prevent potential crashes during rapid interaction with the browser.

### 3. UI and Accessibility Polish
- **Button Visual States:** Custom styles for Primary, Ghost, Icon, and Nav buttons now have explicit templates for Hover, Pressed, and Disabled states. No more "disappearing" buttons.
- **Accessibility:** Navigation buttons in the Browse tab now include Automation names for screen readers.

### 4. CI/CD and Documentation
- The release ZIP now includes standard documentation (`README`, `LICENSE`, `CONTRIBUTING`) plus new `INSTALL.txt` and `QUICKSTART.txt` guides.
- CI workflows now trigger correctly on both `main` and `master` branches.

## Technical Changes
- Updated `OpenIvExecutor` to use `RollbackAction` tracking.
- Added `IsBrowserReady` to `BrowseViewModel`.
- Enhanced `Resources/Styles.xaml` with `ControlTemplates`.
- Bumped project version to `3.6.1`.

## Contributors
- Antigravity AI
- rolling-codes
