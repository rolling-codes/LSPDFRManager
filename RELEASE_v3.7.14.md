# Release v3.7.14 - Uninstall Delete Safety Patch

Urgent patch release for uninstall flows that could remove the library record even when detected files were not actually deleted.

---

## Fixes

- Track uninstall outcomes for deleted, missing, shared, and failed files.
- Delete both active and `.disabled` detected files during uninstall.
- Keep the mod in the library when one or more files cannot be deleted.
- Surface a clear uninstall error on the mod card when deletion fails.
- Preserve shared files used by other installed mods and report them as skipped, not failed.

---

## Verification

```
dotnet build LSPDFRManager.sln --no-restore /m:1
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --no-build
```

Result: 811 passing.

Packaged EXE startup smoke passed from the release folder.

**Full Changelog**: https://github.com/rolling-codes/LSPDFRManager/compare/v3.7.13...v3.7.14
