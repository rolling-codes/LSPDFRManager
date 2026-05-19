namespace LSPDFRManager.LocalApi.Dtos;

public record InstalledModDto(
    Guid Id, string Name, string Type, string TypeColor, string TypeLabel,
    bool IsEnabled, bool IsFavorite, bool HasConflict,
    string Version, string Author,
    string InstalledAt, long TotalSizeBytes, string TotalSizeDisplay,
    int DetectionScore, string Notes, string? ImageUrl, string? ThumbnailUrl,
    int LoadOrderPriority);

public record ModsListResponse(IReadOnlyList<InstalledModDto> Mods, int Total);

public record ToggleModRequest(bool Enabled);

public record UpdateModNotesRequest(string Notes);
