# Source Overview

This repository contains the full source for LSPDFR Manager — a .NET 8 WPF desktop app.

## Entry Points

| File | Purpose |
|:-----|:--------|
| `App.xaml` / `App.xaml.cs` | WPF application entry, resource registration |
| `MainWindow.xaml` / `.cs` | Shell window, tab/nav routing |

## Source Folders

| Folder | Contents |
|:-------|:---------|
| `Views/` | XAML views, one per tab (BrowseView, InstallView, DiagnosticsView, …) |
| `ViewModels/` | MVVM view models, INotifyPropertyChanged via ObservableObject |
| `Core/` | Install engine, XmlPatcher, OpenIvExecutor, mod detection logic |
| `Services/` | FileInstaller, ModDownloadBridge, diagnostics, path detection |
| `Domain/` | Shared models (ModInfo, InstallResult, XmlPatch, …) |
| `Resources/` | Styles.xaml, Colors.xaml, app icon |
| `Converters/` | WPF value converters (StringToBrush, InverseBool, …) |
| `LSPDFRManager.Api/` | Background API service used by the browser install flow |
| `LSPDFRManager.Tests/` | xUnit test suite — run with `dotnet test` |
| `Domain/ModConfigFile.cs` | Model for mod-owned config files (vehicles.meta, etc.) |
| `Domain/OivPackage.cs` | OIV package manifest model + file entry types |
| `Services/ModConfigService.cs` | Load/validate/backup/save mod config XML files |
| `Services/OivService.cs` | Create, parse, preview, and install .oiv packages |
| `ViewModels/ModConfigViewModel.cs` | MVVM VM for the Mod Config editor tab |
| `ViewModels/OivViewModel.cs` | MVVM VM for OIV creator/installer tab |
| `Views/ModConfigView.xaml` | Mod Config editor view |
| `Views/OivView.xaml` | OIV creator/installer view |

## Build

```bash
dotnet restore LSPDFRManager.sln
dotnet build LSPDFRManager.sln --configuration Release
dotnet test LSPDFRManager.Tests/LSPDFRManager.Tests.csproj --configuration Release
```

## Release Packaging

`.github/workflows/dotnet.yml` handles CI and release:
- Triggers on push/PR to `main` or `master`, and on `v*` tags
- Publishes a self-contained-false win-x64 binary to `publish/`
- Copies `INSTALL.txt` and `LICENSE` into the publish folder
- Zips to `LSPDFRManager-vX.Y.Z-win-x64.zip` and creates a GitHub Release
