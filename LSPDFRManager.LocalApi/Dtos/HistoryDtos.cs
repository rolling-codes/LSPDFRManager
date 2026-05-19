namespace LSPDFRManager.LocalApi.Dtos;

public record ChangeHistoryEntryDto(
    string Id,
    string Action,
    string Description,
    string? AffectedFile,
    string? Detail,
    DateTime OccurredAt);

public record HistoryResponse(
    IReadOnlyList<ChangeHistoryEntryDto> Entries,
    int Total);
