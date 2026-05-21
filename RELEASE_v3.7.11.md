# Release v3.7.11 — Tab Navigation Crash Fixes

Patch release resolving two `XamlParseException` crashes that occurred when navigating to the Install or Mod Config tabs.

## Bug Fixes

- **Install tab crash** (`InstallView.xaml`) — `TextPrimaryBrush` and `TextSecondaryBrush` were undefined `StaticResource` keys. Replaced with `TextPrimary` and `TextSubtle`.
- **Mod Config tab crash** (`ConfigView.xaml`) — `BgAlt` was an undefined `StaticResource` key at 4 locations (grid splitter and panel backgrounds). Replaced with `PanelAltBackground`.

Both crashes surfaced as `XamlParseException` on first navigation to the affected tab and were caused by resource key names that do not exist in `Resources/Colors.xaml` or `Resources/Styles.xaml`.

## Verification

```
dotnet build LSPDFRManager.sln          # 0 errors
dotnet test LSPDFRManager.Tests/...     # 734/734 passed
```

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.10...v3.7.11
