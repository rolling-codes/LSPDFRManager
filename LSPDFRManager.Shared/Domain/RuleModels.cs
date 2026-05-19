namespace LSPDFRManager.Domain;

/// <summary>How dangerous an auto-fix is. Drives the backup-first requirement.</summary>
public enum FixRisk
{
    /// <summary>No files changed — safe to apply without backup.</summary>
    None,
    /// <summary>Reads or copies only — no destructive changes.</summary>
    ReadOnly,
    /// <summary>Modifies files but is reversible (backup created first).</summary>
    Reversible,
    /// <summary>Modifies files in a way that may be hard to undo.</summary>
    Destructive,
}

/// <summary>A human-readable suggestion for resolving a rule finding.</summary>
/// <param name="Text">Action the user should take.</param>
/// <param name="Risk">How risky the suggested action is.</param>
/// <param name="CanAutoFix">True if the app can perform this fix automatically (with preview + backup).</param>
public sealed record SuggestedFix(string Text, FixRisk Risk = FixRisk.None, bool CanAutoFix = false);

/// <summary>Result of evaluating one rule against its context.</summary>
/// <param name="Passed">True if the rule condition is satisfied (no problem found).</param>
/// <param name="Title">Short label displayed in diagnostics UI.</param>
/// <param name="Detail">Full explanation of what was found and why it matters.</param>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Fixes">Ordered list of suggested remediation steps.</param>
public sealed record RuleResult(
    bool Passed,
    string Title,
    string Detail,
    DiagnosticSeverity Severity = DiagnosticSeverity.Info,
    IReadOnlyList<SuggestedFix>? Fixes = null)
{
    public static RuleResult Pass(string title) =>
        new(true, title, string.Empty, DiagnosticSeverity.Info);

    public static RuleResult Fail(
        string title,
        string detail,
        DiagnosticSeverity severity,
        params SuggestedFix[] fixes) =>
        new(false, title, detail, severity, fixes);
}

/// <summary>
/// Deterministic, individually-testable rule.
/// Implementations must be stateless — all inputs come via <typeparamref name="TContext"/>.
/// </summary>
public interface IRule<TContext>
{
    /// <summary>Stable, kebab-case identifier (e.g. "missing-lspdfr-dll").</summary>
    string Id { get; }
    string Title { get; }
    DiagnosticSeverity Severity { get; }
    RuleResult Evaluate(TContext context);
}
