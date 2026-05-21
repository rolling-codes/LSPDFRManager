namespace LSPDFRManager.Domain;

/// <summary>
/// Full aggregated output of <c>PatrolReadinessController.ScanAsync()</c>.
/// All lists are non-null; empty when nothing found.
/// </summary>
public sealed record PatrolReadinessSummary(
    PatrolReadinessState Status,

    /// <summary>0–100. Starts at 100; each blocker −20, each warning −5 (floor 0).</summary>
    int Score,

    IReadOnlyList<InstallIssue> BlockingIssues,
    IReadOnlyList<InstallIssue> Warnings,
    IReadOnlyList<InstallIssue> Info,

    /// <summary>Deduplicated top-priority fixes across all scanners.</summary>
    IReadOnlyList<SuggestedFix> SuggestedFixes,

    DateTimeOffset ScannedAt,

    // ── Source breakdowns ─────────────────────────────────────────────────
    PatrolReadinessResult? CoreChecks,
    IReadOnlyList<ModHealthResult> ModHealthSummary,
    IReadOnlyList<DllDuplicateResult> DuplicateDllSummary,
    IReadOnlyList<LintFinding> ConfigLintSummary,
    KnownGoodDiff? KnownGoodDiffSummary)
{
    public static PatrolReadinessSummary Empty { get; } = new(
        PatrolReadinessState.Unknown, 100,
        [], [], [], [],
        DateTimeOffset.UtcNow,
        null, [], [], [], null);
}
