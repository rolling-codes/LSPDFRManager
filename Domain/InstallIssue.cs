namespace LSPDFRManager.Domain;

/// <summary>
/// A single issue surfaced by <c>PatrolReadinessController</c>.
/// Immutable — created by scanners, consumed by the ViewModel.
/// </summary>
/// <param name="Code">Stable kebab-case identifier (e.g. "missing-lspdfr-dll").</param>
/// <param name="Title">Short label shown in the dashboard list.</param>
/// <param name="Detail">Full explanation of what was found and why it matters.</param>
/// <param name="Source">Which scanner produced this issue (e.g. "PatrolReadiness", "DllDuplicate").</param>
/// <param name="Fixes">Ordered suggested remediation steps.</param>
public sealed record InstallIssue(
    string Code,
    string Title,
    string Detail,
    string Source,
    IReadOnlyList<SuggestedFix> Fixes);
