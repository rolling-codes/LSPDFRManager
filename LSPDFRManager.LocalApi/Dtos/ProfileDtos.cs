namespace LSPDFRManager.LocalApi.Dtos;

public record ModProfileDto(string Id, string Name, string? Notes, string CreatedAt, string? LastUsedAt, int EntryCount);

public record ProfilesListResponse(IReadOnlyList<ModProfileDto> Profiles, string? ActiveProfileId);

public record CreateProfileRequest(string Name, string? Notes);

public record UpdateProfileRequest(string? Name, string? Notes);
