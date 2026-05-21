using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class ProfileEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void MapProfiles(this WebApplication app)
    {
        app.MapGet("/api/v1/profiles", () =>
        {
            try
            {
                var profiles = LoadProfiles();
                var dtos = profiles.Select(ToDto).ToList();
                var activeId = AppConfig.Instance.ActiveProfileId;
                return Results.Ok(new ProfilesListResponse(dtos, activeId));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read profiles: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/profiles", (CreateProfileRequest request) =>
        {
            try
            {
                var profile = new ModProfile
                {
                    Name = request.Name,
                    Notes = request.Notes,
                };
                SaveProfile(profile);
                return Results.Ok(ToDto(profile));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create profile: {ex.Message}");
            }
        });

        app.MapPut("/api/v1/profiles/{id}", (string id, UpdateProfileRequest request) =>
        {
            try
            {
                var path = ProfilePath(id);
                if (!File.Exists(path))
                    return Results.NotFound($"Profile {id} not found.");

                var json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<ModProfile>(json, JsonOpts);
                if (profile is null)
                    return Results.NotFound($"Profile {id} not found.");

                if (request.Name is not null) profile.Name = request.Name;
                if (request.Notes is not null) profile.Notes = request.Notes;

                SaveProfile(profile);
                return Results.Ok(ToDto(profile));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update profile: {ex.Message}");
            }
        });

        app.MapDelete("/api/v1/profiles/{id}", (string id) =>
        {
            try
            {
                var path = ProfilePath(id);
                if (!File.Exists(path))
                    return Results.NotFound($"Profile {id} not found.");

                File.Delete(path);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete profile: {ex.Message}");
            }
        });
    }

    private static List<ModProfile> LoadProfiles()
    {
        var dir = AppDataPaths.ProfilesDirectory;
        if (!Directory.Exists(dir)) return [];
        var result = new List<ModProfile>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var p = JsonSerializer.Deserialize<ModProfile>(json, JsonOpts);
                if (p != null) result.Add(p);
            }
            catch { }
        }
        return result;
    }

    private static string ProfilePath(string id) =>
        Path.Combine(AppDataPaths.ProfilesDirectory, $"{id}.json");

    private static void SaveProfile(ModProfile p)
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        File.WriteAllText(ProfilePath(p.Id), JsonSerializer.Serialize(p));
    }

    private static ModProfileDto ToDto(ModProfile p) =>
        new(p.Id, p.Name, p.Notes,
            p.CreatedAt.ToString("o"),
            p.LastUsedAt?.ToString("o"),
            p.Entries.Count);
}
