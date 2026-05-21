# Release v3.7.8 — Mod intelligence in the review screen

## Highlights

The detection intelligence introduced in v3.7.7 (#25, #26) is now visible at the exact decision point where it matters: the pre-install review screen.

## Mod type and dependency display in the review panel (#28)

- Detected primary mod type is shown as a badge (e.g. "LSPDFR Plugin", "ASI Mod", "Script") alongside a confidence label (High / Medium / Low).
- Mixed archives show a "MIXED" indicator and list each secondary detected type as a pill.
- Unknown archives display a neutral message: "Could not determine mod type — review files manually."
- Detection evidence is shown in a collapsed expander — visible on demand, hidden by default to keep the primary review path uncluttered.
- Dependency warnings (Script Hook V, SHVDN, ASI Loader, LSPDFR, RAGE Plugin Hook, OpenIV, etc.) appear under a "REQUIRED DEPENDENCIES" heading, with internal prefixes stripped for display.
- General warnings (path conflicts, overwrites, suspicious entries) remain in their own section below, unchanged.

## Architecture

- `InstallPlan` now carries `ModTypeDetectionResult` so the VM can surface detection intelligence without re-running the classifier.
- `InstallViewModel` exposes 13 computed properties (type label, confidence, mixed flag, secondary types, evidence, dependency warnings, general warnings) as view-model projections with full property-change notification.

## Installed dependency probe service (#29)

- Probes the selected GTA V install folder during review-plan generation and classifies detected dependencies as Present, Missing, Unknown, or NotApplicable.
- Adds dedicated probes for Script Hook V, ScriptHookVDotNet, ASI Loader, LSPDFR, RAGE Plugin Hook, and OpenIV-related package handling.
- Groups dependency probe results in the review panel by status: green for present, red for missing, and amber for unknown.
- Treats OpenIV as NotApplicable for local probing because OIV packages require package-aware installation rather than a normal in-folder dependency check.

## OIV package handling guardrail (#30)

- Blocks primary OIV packages from being installed as loose files.
- Warns when OIV content is detected as a secondary/mixed mod type.
- Adds explicit review banners for OIV packages: primary OIV packages show "OIV PACKAGE — CANNOT INSTALL AS LOOSE FILES," while secondary OIV content shows "OIV CONTENT DETECTED."
- Centralizes OIV safety copy in `InstallerSafetyPolicy` so the planner, ViewModel, and tests use consistent messages.
- Keeps non-OIV install flows unchanged.

## Native OIV package inspection (#31)

- Parses `assembly.xml` at plan time to surface package name, version, author, target game, and file count in the review panel alongside the block banner.
- `OivService.ParseFromStream` handles stream-based parsing; malformed manifests produce a planner warning and a graceful no-metadata state rather than a crash.
- Fixed OIV misclassification: `assembly.xml` at root now scores at maximum confidence so OIV packages containing DLC content (`dlcpacks/`, `.rpf`) are not misidentified as Vehicle DLC.
- XML parsing hardened against untrusted input: DTD prohibited, 1 MB / 1M-character size cap enforced on both seekable and non-seekable streams, `XmlReader` settings include `CloseInput=false` (caller-owned stream never closed by reader disposal), `IgnoreComments`, and `IgnoreProcessingInstructions`.

## Verification

- 654/654 tests passing.
- Clean build.

## Issues closed

- #28 Review panel: mod type + dependency warning display
- #29 Installed dependency probe service
- #30 OIV package handling guardrail
- #31 Native OIV package inspection (read-only)
