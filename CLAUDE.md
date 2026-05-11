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

**Build release ZIP (framework-dependent)**
```bash
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.6.0 -p:DebugType=None -p:DebugSymbols=false
New-Item -ItemType Directory -Path release-package/LSPDFRManager-v3.6.0 -Force
Copy-Item -Path publish/v3.6.0/* -Destination release-package/LSPDFRManager-v3.6.0 -Recurse
Compress-Archive -Path release-package/LSPDFRManager-v3.6.0 -DestinationPath LSPDFRManager-v3.6.0-win-x64.zip
```

## Testing

```bash
dotnet test                          # all tests
dotnet test --filter "ClassName"     # single test class
dotnet test -v detailed              # verbose output
```

Test suite uses real temp directories — no mocked file I/O, no mocked services.

## Further Reading

- [Architecture](docs/architecture.md) — layers, singletons, core workflows, storage, dependencies
- [Installer Safety](docs/installer-safety.md) — safety contract, rollback guarantee, phase gates, anti-patterns
- [Common Tasks & Gotchas](docs/common-tasks.md) — how to add mod types/settings, debug installs, known pitfalls
- [Troubleshooting](docs/troubleshooting.md)
