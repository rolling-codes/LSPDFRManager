namespace LSPDFRManager.LocalApi.Dtos;

public record ComponentVersionDto(
    string Name,
    bool Present,
    string? Version,
    string? Hash);

public record CompatibilityResponse(
    IReadOnlyList<ComponentVersionDto> Components,
    bool GtaPathConfigured,
    DateTime DetectedAt);
