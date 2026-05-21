namespace LSPDFRManager.OivPipeline.Models;

public sealed record ValidationGate(string Name, bool Passed, string? Reason = null);

public sealed class ValidationResult
{
    public bool Passed { get; init; }
    public IReadOnlyList<ValidationGate> Gates { get; init; } = [];
    public IReadOnlyList<string> RefusalReasons { get; init; } = [];
}
