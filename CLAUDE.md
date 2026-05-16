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
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.7.6 -p:DebugType=None -p:DebugSymbols=false
New-Item -ItemType Directory -Path release-package/LSPDFRManager-v3.7.6 -Force
Copy-Item -Path publish/v3.7.6/* -Destination release-package/LSPDFRManager-v3.7.6 -Recurse
Compress-Archive -Path release-package/LSPDFRManager-v3.7.6 -DestinationPath LSPDFRManager-v3.7.6-win-x64.zip
```

## Testing

**Run all tests**
```bash
dotnet test
```

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
- Singletons (`AppConfig`, `ModLibraryService`) must be reset in tests — call `Instance = null` (or equivalent reset) before and after each test class
- No mocking of file I/O or singletons in tests — use real temp directories and JSON serialization

## Installer Safety (Hard Constraint)

The installer must **never** leave the filesystem in a partially-installed state. The extraction sequence is non-negotiable:
1. `PathSafety.GetSafePath()` first
2. Create directory
3. Copy stream → file
4. Add to rollback list **only after** successful copy

External archive libraries (SharpCompress) must stay behind the `IArchive`/`IArchiveEntry` adapter boundary — no SharpCompress types outside the adapter layer. Use `FakeArchive`/`FakeArchiveEntry`/`ThrowingStream` for unit tests; real archives are Phase B only.

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Current release notes: [RELEASE_v3.7.6.md](RELEASE_v3.7.6.md)

## Focus Files

@docs/architecture.md
@docs/workflows.md
@docs/installer-safety.md
@docs/common-tasks.md
@docs/troubleshooting.md
