# LSPDFR Manager v3.7.6

This release delivers the **EUP Outfit Discovery**, **Smart Install Planner**, and **Browse thumbnail extraction** features. Theme: _Know your uniforms. Install smarter. Browse richer._

## Highlights

- **EUP Outfit Discovery** — scans all EUP INI and XML config files under the GTA root and builds a structured list of uniform definitions with department, county, region, gender, and confidence scoring.
- **Smart Install Planner** — runs before every install to detect overwrite conflicts, order Stop The Ped before Ultimate Backup when both are present, surface shared dependency sequencing, and flag path traversal attempts before any file is written.
- **Installer Safety Policy** — all per-plugin install rules (STP/UB ordering, known backup config detection, overwrite risk classification) are consolidated in `InstallerSafetyPolicy` as a single authoritative policy layer.
- **LSPDFR Thumbnail Extractor** — extracts og:image / twitter:image / first `<img>` thumbnails from LSPDFR browse pages, with a 256-entry LRU cache and host allowlist (lcpdfr.com, lspdfr.com only).
- **Backup Unit Filter** — filters `BackupUnitDefinition` lists by department, county, gender, and unit category with EUP gender inference.

## Install Safety Details

- `SmartInstallPlanner.BuildPlan()` resolves conflicts, prioritizes file ordering, and surfaces overwrite risks before `FileInstaller` writes anything.
- `InstallerSafetyPolicy.GetInstallOrderPriority()` guarantees shared dependencies → Stop The Ped → Ultimate Backup ordering when all are present in the same batch.
- Existing backup configs (UltimateBackup.ini, DefaultRegions.xml, backup.xml, agency.xml, etc.) default to `RenameIncoming` — never silently overwritten.
- `BackupAndReplace` creates a timestamped `.bak.yyyyMMdd-HHmmss` copy before writing, with collision-safe numbering.
- Rollback restores overwritten originals and removes new files after any install failure.

## EUP Discovery Details

- `EupOutfitDiscoveryService` scans `plugins/EUP/`, `plugins/lspdfr/`, `lspdfr/data/`, `EUP/`, and well-known preset file locations.
- `EupInferenceHelper` infers department (LSPD, BCSO, SAHP, FIB, etc.), county/region, and gender from ped model, section name, and folder path.
- Freemode ped detections (`mp_m_freemode_01`, `mp_f_freemode_01`) receive higher confidence scores (0.85 INI, 0.80 XML).

## Validation

- `dotnet build LSPDFRManager.sln` — 0 errors
- `dotnet test` — 450/450 passing

## Known Non-Blockers

- EUP Outfit Discovery UI panel: deferred.
- Smart Install Planner UI confirmation flow: deferred; planner is fully callable and tested via services.
- Thumbnail display in BrowseView: deferred (extractor is wired but display binding pending).

## Download

- `LSPDFRManager-v3.7.6-win-x64.zip`
