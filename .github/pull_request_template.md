## Summary

- 

## Control-Layer / Install Safety Checklist

- [ ] `dotnet build LSPDFRManager.sln`
- [ ] `dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj`
- [ ] `dotnet test`
- [ ] Architecture guards pass and any new side-effect boundary has a focused test.
- [ ] ViewModels delegate orchestration to controllers/commands; no direct install enqueue from ViewModels.
- [ ] Passive events only stage/update status; installs require explicit user action.
- [ ] Installer safety sequence is unchanged: safe path, create directory, copy stream, then rollback entry.
- [ ] Singleton event subscriptions are detached or scoped.
- [ ] SharpCompress usage stays behind archive adapters.
- [ ] Docs/release notes use relative paths, not machine-local absolute paths.

## UI Smoke

Record one:

- `UI smoke run: done by <name> on <yyyy-mm-dd>`
- `UI smoke run: not run: no WPF/WebView UI available`

Checklist: [docs/ui-smoke-pr-check.md](../docs/ui-smoke-pr-check.md)

## Reviewer Focus

- Search for `Enqueue`, installer calls, passive-event side effects, event subscriptions, and archive-library leakage.
