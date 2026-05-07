# LSPDFR Manager v1.5.0

**Major UI Overhaul & Performance Stabilization**

This release introduces a modern, high-contrast dark theme, improved information hierarchy, and critical fixes for library management stability.

---

## 🎨 UI Overhaul

- **Sophisticated Dark Theme**: Redesigned color palette and typography for a professional, focused experience.
- **Modern Control Templates**: Rounded corners, consistent padding, and improved interactive states for all buttons, inputs, and toggles.
- **Redesigned Sidebar**: Sleeker navigation with integrated environment status indicators and improved branding.
- **Information-Dense Library**:
    - **Overhauled Mod Cards**: Clear visual hierarchy with prominent status and risk badges.
    - **Enhanced Detail Panel**: Grouped metadata, improved spacing, and better handling of optional fields.
- **Improved Views**:
    - **Modern Install Tab**: Redesigned drag-and-drop zone with visual cues and a stylized installation log.
    - **Streamlined Settings**: Grouped configuration sections for improved discoverability.

## 🚀 New Features

- **Mod Notes**: Add and persist personal notes for installed mods directly from the library.
- **Detailed Conflict Reporting**: Displays exact file or DLC conflicts in the detail panel.
- **Auto-Launch LSPDFR**: Optional launch via RAGEPluginHook after successful installation.
- **Risk Filter Indicators**: Visual feedback for active filters in the library view.

## 🛠️ Performance & Stability

- **Flicker-Free Library**: Refactored the library list to update in-place, preserving selection and scroll position.
- **Deterministic File Tracking**: Replaced directory scanning with an exact manifest of written files.
- **Enhanced Safety**: Optional confirmation before mod uninstallation (enabled by default).
- **Version Alignment**: Sidebar displays the correct application version (v1.5.0).

---

## 📦 Technical Changes

- Modified `FileInstaller` to return a precise `WrittenFiles` list.
- Implemented `ObservableCollection` in-place update logic in `LibraryViewModel`.
- Added `StringToVisibilityConverter` for cleaner XAML logic.
- Unified enable/disable logic in `ModLibraryService`.

---

## Download & Update

Execute `run.bat` or use the quick-install command in the README to install the latest version.
