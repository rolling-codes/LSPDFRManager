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

- Probes the GTA V install folder at plan time to classify each detected dependency as Present, Missing, Unknown, or NotApplicable.
- Script Hook V, SHVDN, ASI Loader, LSPDFR, RAGE Plugin Hook, and OpenIV all have dedicated probe rules.
- Review panel groups probes by status: green for present, red for missing, amber for unknown.
- OpenIV is always NotApplicable — it requires a separate installer regardless.

## OIV package handling guardrail (#30)

- Primary OIV packages are blocked from loose-file install — the Confirm Install button is disabled and the review panel shows a clear explanation.
- Archives with OIV as a secondary/mixed type show a warning banner; the install is not blocked but the user is directed to OpenIV or a compatible package installer.
- Non-OIV installs are unaffected.

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
