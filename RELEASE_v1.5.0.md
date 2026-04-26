# LSPDFR Manager v1.5.0

**Major UI Overhaul & Performance Stabilization**

This release transforms LSPDFR Manager with a modern, high-contrast dark theme, improved information hierarchy, and critical fixes for library management stability.

---

## 🎨 UI Overhaul

- **Sophisticated Dark Theme**: Redesigned color palette and typography for a professional, focused experience.
- **Modern Control Templates**: Rounded corners, consistent padding, and improved interactive states for all buttons, inputs, and toggles.
- **Redesigned Sidebar**: Sleeker navigation with integrated environment status indicators and improved branding.
- **Information-Dense Library**:
    - **Overhauled Mod Cards**: More clear visual hierarchy with prominent status and risk badges.
    - **Enhanced Detail Panel**: Grouped metadata, improved spacing, and better handling of optional fields.
- **Improved Views**:
    - **Modern Install Tab**: Redesigned drag-and-drop zone with visual cues and a stylized installation log.
    - **Streamlined Settings**: Grouped configuration sections for better discoverability.

## 🚀 New Features

- **Mod Notes**: Add and persist personal notes for any installed mod directly from the library.
- **Detailed Conflict Reporting**: See exactly which files or DLC packs are causing conflicts in the detail panel.
- **Auto-Launch LSPDFR**: New preference to automatically launch the game via RAGEPluginHook after a successful installation.
- **Risk Filter Indicators**: Visual feedback for active filters in the library view.

## 🛠️ Performance & Stability

- **Flicker-Free Library**: Refactored the library list to update in-place, preserving selection and scroll position during searches or filters.
- **Deterministic File Tracking**: Replaced imprecise directory scanning with an exact manifest of files written during installation.
- **Enhanced Safety**: Added optional (enabled by default) confirmation before mod uninstallation.
- **Version Alignment**: Sidebar now correctly displays the current application version (v1.5.0).

---

## 📦 Technical Changes

- Modified `FileInstaller` to return a precise `WrittenFiles` list.
- Implemented `ObservableCollection` in-place update logic in `LibraryViewModel`.
- Added `StringToVisibilityConverter` for cleaner XAML logic.
- Unified enable/disable logic in `ModLibraryService` as the single source of truth.

---

## Download & Update

Run `run.bat` or use the quick-install one-liner in the README to update to the latest version.
