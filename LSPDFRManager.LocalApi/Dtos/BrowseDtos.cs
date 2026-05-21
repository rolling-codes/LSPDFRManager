namespace LSPDFRManager.LocalApi.Dtos;

public record BrowseSearchRequest(string Query, int Page = 1);

public record BrowseModDto(
    string Id,
    string Name,
    string? Author,
    string? Description,
    string? Version,
    string? ImageUrl,
    string? DownloadUrl,
    string? PageUrl
);

public record BrowseSearchResponse(
    IReadOnlyList<BrowseModDto> Results,
    int Page,
    int TotalResults,
    bool HasMore
);
