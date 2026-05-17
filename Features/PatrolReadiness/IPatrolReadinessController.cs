using LSPDFRManager.Domain;

namespace LSPDFRManager.Features.PatrolReadiness;

/// <summary>
/// Aggregates all readiness scanners and returns a single <see cref="PatrolReadinessSummary"/>.
/// Implementations must be read-only — no file writes, installs, or destructive actions.
/// </summary>
public interface IPatrolReadinessController
{
    Task<PatrolReadinessSummary> ScanAsync(CancellationToken ct = default);
    void MarkKnownGood();
}
