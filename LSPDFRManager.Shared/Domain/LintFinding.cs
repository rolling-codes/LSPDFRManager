namespace LSPDFRManager.Domain;

/// <summary>A single finding from the config linter.</summary>
/// <param name="FilePath">Absolute path to the config file.</param>
/// <param name="Line">1-based line number, or null if not line-specific.</param>
/// <param name="Section">INI section or JSON path, if applicable.</param>
/// <param name="Key">The key or field involved.</param>
/// <param name="Code">Stable, kebab-case finding code (e.g. "duplicate-key", "invalid-xml").</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Severity">How serious the finding is.</param>
public sealed record LintFinding(
    string FilePath,
    int? Line,
    string? Section,
    string? Key,
    string Code,
    string Message,
    DiagnosticSeverity Severity);
