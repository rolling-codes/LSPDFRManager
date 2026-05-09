namespace LSPDFRManager.Api.Models;

public record ModSummary(
    string Id,
    string Title,
    string Author,
    string Version,
    string Type,
    string Description,
    string Url,
    string DownloadUrl,
    string ImageUrl,
    string UpdatedAt
);
