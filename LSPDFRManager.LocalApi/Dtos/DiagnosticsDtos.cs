namespace LSPDFRManager.LocalApi.Dtos;

public record DiagnosticFindingDto(
    string Category,
    string Title,
    string? Detail,
    string? RecommendedFix,
    string? AffectedPath,
    string Severity);

public record DiagnosticsResponse(
    IReadOnlyList<DiagnosticFindingDto> Findings,
    int TotalFindings,
    int ErrorCount,
    int WarningCount);
