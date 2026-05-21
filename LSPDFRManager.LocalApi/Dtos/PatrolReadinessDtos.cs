namespace LSPDFRManager.LocalApi.Dtos;

public record PatrolReadinessResultDto(
    IReadOnlyList<string> Blocking,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Passing,
    bool IsReady
);
