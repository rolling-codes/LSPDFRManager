namespace LSPDFRManager.Domain;

public enum DependencyProbeStatus { Present, Missing, Unknown, NotApplicable }

public class DependencyProbe
{
    public required string Name { get; init; }
    public required DependencyProbeStatus Status { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = [];
    public required string Message { get; init; }
}

public class DependencyProbeResult
{
    public static readonly DependencyProbeResult Empty = new() { Probes = [] };
    public IReadOnlyList<DependencyProbe> Probes { get; init; } = [];
    public bool HasMissingRequired => Probes.Any(p => p.Status == DependencyProbeStatus.Missing);
    public bool HasUnknown => Probes.Any(p => p.Status == DependencyProbeStatus.Unknown);
}
