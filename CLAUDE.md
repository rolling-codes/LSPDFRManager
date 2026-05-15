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
dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/v3.7.5 -p:DebugType=None -p:DebugSymbols=false
New-Item -ItemType Directory -Path release-package/LSPDFRManager-v3.7.5 -Force
Copy-Item -Path publish/v3.7.5/* -Destination release-package/LSPDFRManager-v3.7.5 -Recurse
Compress-Archive -Path release-package/LSPDFRManager-v3.7.5 -DestinationPath LSPDFRManager-v3.7.5-win-x64.zip
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

## Repository

- Repository: https://github.com/rolling-codes/LSPDFRManager
- Current release notes: [RELEASE_v3.7.6.md](RELEASE_v3.7.6.md)

## Focus Files

@docs/architecture.md
@docs/workflows.md
@docs/installer-safety.md
@docs/common-tasks.md
@docs/troubleshooting.md
