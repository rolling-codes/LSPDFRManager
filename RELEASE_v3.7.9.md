# Release v3.7.9 — OIV Creator MVP

## Highlights

Users can now author valid `.oiv` packages directly inside LSPDFR Manager through a three-step wizard — no OpenIV, no scripting, no manual XML editing required.

## OIV Creator wizard (#32)

A plan-based creator pipeline replaces the old file-by-file creator with a reviewable, validated workflow.

### Wizard steps

1. **Edit** — Add source files (individual or folder), fill package name, version, author, and description, choose an output path.
2. **Review Plan** — The scanner walks the source files, the validator checks for errors (missing name, empty file list, duplicate install paths, path traversal), and the plan is displayed with file count, total size, per-file install paths, and any warnings. Errors block export; warnings are advisory only.
3. **Done** — The builder assembles the `.oiv` ZIP with `assembly.xml` at root and all content files under `content/`. A success checkmark and file count are shown; a "Create Another Package" button resets the wizard.

### Safety constraints enforced at scan time

- Executable and script file types (`.exe`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.sh`, `.msi`, `.com`, `.scr`) are rejected with a warning and never included in the package.
- Path traversal patterns (`..`) in install paths are reported as errors and block export.
- Source files that have disappeared since the scan are caught at validation time.
- The builder writes only to the caller-supplied output path. It never reads or writes the GTA V install folder.

### OIV format compliance

- Format version `2.2`, `target="Five"` (correct OIV spec target for GTA V).
- `assembly.xml` is UTF-8 without BOM; all user-supplied strings are XML-escaped.
- Content files are stored at `content/<install-path>` inside the ZIP.
- Version string is split into `<major>`/`<minor>` elements.

### Architecture

| New type | Role |
|---|---|
| `OivPackageKind` | Enum: Basic, DlcPack, EngineSound, AddOnVehicle, ReplaceVehicle |
| `OivPackageFile` | Immutable record: SourcePath, InstallPath, SizeBytes |
| `OivPackagePlan` | Immutable record carrying metadata + file list + Errors + Warnings |
| `IOivSourceScanner` / `OivSourceScanner` | Walks source paths, filters blocked extensions |
| `IOivPackageValidator` / `OivPackageValidator` | Validates plan, returns derived plan with findings |
| `IOivAssemblyXmlWriter` / `OivAssemblyXmlWriter` | Generates `assembly.xml` content |
| `IOivPackageBuilder` / `OivPackageBuilder` | Assembles final `.oiv` ZIP asynchronously |

`OivViewModel` retains the existing Installer mode unchanged. The Creator mode is replaced with the wizard pipeline; `IsCreatorStep0/1/2` drive XAML visibility per wizard step.

## Verification

- 679/679 tests passing.
- Clean build.

## Issues closed

- #32 OIV Creator MVP

## Future issues (not in this release)

- #33 DLC Pack OIV Creator
- #34 Engine Sound OIV Creator
- #35 Add-on Vehicle OIV Creator
- #36 Replace Vehicle OIV Creator
- #37 OIV Creator Templates
