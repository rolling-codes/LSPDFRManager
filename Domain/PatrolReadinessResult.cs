namespace LSPDFRManager.Domain;

public enum PatrolReadinessState { Ready, Warning, NotReady, Unknown }

public class PatrolReadinessResult
{
    public PatrolReadinessState OverallState { get; init; }
    public List<string> BlockingIssues { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> PassingChecks { get; init; } = [];
    public DateTime CheckedAtUtc { get; init; } = DateTime.UtcNow;

    public static PatrolReadinessState ComputeState(
        IReadOnlyList<string> blocking,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> passing) =>
        blocking.Count > 0 ? PatrolReadinessState.NotReady :
        warnings.Count > 0 ? PatrolReadinessState.Warning :
        passing.Count > 0  ? PatrolReadinessState.Ready :
                             PatrolReadinessState.Unknown;
}
