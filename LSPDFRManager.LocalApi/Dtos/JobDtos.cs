namespace LSPDFRManager.LocalApi.Dtos;

public record JobStatusDto(
    string JobId,
    string State,
    int ProgressPct,
    string? Error,
    string? ResultJson);
