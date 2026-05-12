# LSPDFR Manager v3.7.3

This release delivers a major UI refresh with an LSPDFR-aligned command-center visual system.

## Highlights

- New app shell styling with a stronger sidebar hierarchy, active-state navigation treatment, and elevated content framing.
- Redesigned **Dashboard (Command Center)** with clearer telemetry, metric cards, and action grouping.
- Redesigned **Library** layout with improved top toolbar framing, risk-filter presentation, and panel readability.

## Visual System

- Adopted the police-blue LSPDFR color scheme as the canonical theme token source in `Resources/Colors.xaml`.
- Added richer token usage and shared reusable styles in `Resources/Styles.xaml`:
  - `ShellSidebar`, `ShellContentPanel`
  - `MetricCard`, `ActionGroupCard`, `TelemetryCard`
  - `StatusChip`, `LibraryToolbarCard`, `LibraryRowCard`
- Improved button templates (primary/ghost/nav) with consistent radius, hover, and active semantics.

## Version Consistency

- Updated project versioning to `3.7.3` in `LSPDFRManager.csproj`.
- Updated the in-app shell version label to `v3.7.3`.

## Known Scope Boundaries

- This release redesigns the shell, Dashboard, and Library views only.
- Remaining views retain current layout structure and will be addressed in later UI phases.

## Validation

- `dotnet restore`
- `dotnet build -c Release`
- `dotnet test`

## Download

- `LSPDFRManager-v3.7.3-win-x64.zip`

