# Release v3.7.13 - Urgent Button Crash Patch

Urgent patch release for UI button crashes discovered during the button press validation pass.

---

## Fixes

- Fixed crashes when opening Patrol Readiness and Developer Diagnostics caused by missing shared XAML resource aliases.
- Fixed a Patrol Readiness binding crash on read-only text properties.
- Fixed Dashboard launch buttons so invalid or unlaunchable `GTA5.exe` / `RAGEPluginHook.exe` targets log a friendly error instead of crashing the UI.
- Added an in-process WPF button wiring smoke test that renders the changed views and executes key commands.

---

## Verification

```
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
```

Result: 807 passing.

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.12...v3.7.13
