# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Build (Debug)**
```bash
dotnet build LSPDFRManager.sln
```

**Run (requires GTA V path configured in settings)**
```bash
dotnet run --project LSPDFRManager.csproj
```

**Build self-contained executable (Release)**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

**Build release ZIP (framework-dependent)** — update version number as needed
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.7.11 -p:DebugType=None -p:DebugSymbols=false
New-Item -ItemType Directory -Path release-package/LSPDFRManager-v3.7.11 -Force
Copy-Item -Path publish/v3.7.11/* -Destination release-package/LSPDFRManager-v3.7.11 -Recurse
Compress-Archive -Path release-package/LSPDFRManager-v3.7.11 -DestinationPath LSPDFRManager-v3.7.11-win-x64.zip
```

> **If `dotnet publish` fails with WPF temp-file copy errors** (race with the IDE holding `obj/`), use msbuild directly:
> ```bash
> dotnet msbuild LSPDFRManager.csproj -t:Publish -p:Configuration=Release -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:PublishDir=publish/v3.7.11 -p:DebugType=None -p:DebugSymbols=false
> ```

## Testing

**Run all tests**
```bash
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj
```

> If `dotnet test` fails with a coverage file lock error (`msCoverageSourceRootsMapping` cannot be read), the VS Code test extension is holding the file. Build with msbuild first, then run with `--no-build`:
> ```bash
> dotnet msbuild LSPDFRManager.Tests/LSPDFRManager.Tests.csproj -p:CollectCoverage=false -v:q
> dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --no-build
> ```

**Run single test class**
```bash
dotnet test --filter "ClassName"
```

**Run with verbose output**
```bash
dotnet test -v detailed
```

## Code Conventions

- File-scoped namespaces: `namespace LSPDFRManager.ViewModels;` (no braces)
- Nullable reference types enabled: `string?` for nullable, `string` for non-null
- Singletons (`AppConfig`, `ModLibraryService`, `TransactionService`) must be reset in tests — call `Instance = null` / `AppDataPaths.OverrideRoot()` before and after each test class
- No mocking of file I/O or singletons in tests — use real temp directories and JSON serialization

## Installer Safety (Hard Constraint)

The installer must **never** leave the filesystem in a partially-installed state. The extraction sequence is non-negotiable:
1. `PathSafety.GetSafePath()` first
2. Create directory
3. Copy stream → file
4. Add to rollback list **only after** successful copy

External archive libraries (SharpCompress) must stay behind the `IArchive`/`IArchiveEntry` adapter boundary — no SharpCompress types outside the adapter layer. Use `FakeArchive`/`FakeArchiveEntry`/`ThrowingStream` for unit tests; real archives are Phase B only.

## Verification (Hard Constraint)

**Never claim a fix or release is complete based on unit tests alone.** Always build and launch the executable to confirm it starts and the UI renders without crashing:

```bash
dotnet build LSPDFRManager.sln
dotnet run --project LSPDFRManager.csproj
```

For UI-affecting changes, run the [UI smoke checklist](docs/ui-smoke-pr-check.md) before marking done.

## Release Packaging

When assembling a release ZIP, include only runtime artifacts — strip `build/`, `obj/`, `publish/`, WebView2 cache directories, and source files. Verify ZIP contents and size before publishing. Use the framework-dependent build above; confirm the unpacked folder launches cleanly before tagging.

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Current release notes: [RELEASE_v3.7.11.md](RELEASE_v3.7.11.md)

## Focus Files

@docs/architecture.md
@docs/workflows.md
@docs/installer-safety.md
@docs/common-tasks.md
@docs/troubleshooting.md
