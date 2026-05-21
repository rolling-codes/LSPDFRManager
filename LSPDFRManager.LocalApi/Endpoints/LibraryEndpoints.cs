using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class LibraryEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void MapLibrary(this WebApplication app)
    {
        app.MapGet("/api/v1/mods", (string? search, string? enabled, string? type) =>
        {
            try
            {
                var mods = LoadLibrary();

                if (!string.IsNullOrWhiteSpace(search))
                    mods = mods.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                if (!string.IsNullOrWhiteSpace(enabled))
                {
                    if (bool.TryParse(enabled, out var isEnabled))
                        mods = mods.Where(m => m.IsEnabled == isEnabled).ToList();
                }

                if (!string.IsNullOrWhiteSpace(type))
                    mods = mods.Where(m => m.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

                var dtos = mods.Select(ToDto).ToList();
                return Results.Ok(new ModsListResponse(dtos, dtos.Count));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read library: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/mods/{id:guid}/enable", (Guid id, ToggleModRequest request) =>
        {
            try
            {
                var mods = LoadLibrary();
                var mod = mods.FirstOrDefault(m => m.Id == id);
                if (mod is null)
                    return Results.NotFound($"Mod {id} not found.");

                new InstalledModFileService().SetEnabled(mod, request.Enabled);
                SaveLibrary(mods);
                return Results.Ok(ToDto(mod));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to toggle mod: {ex.Message}");
            }
        });

        app.MapPut("/api/v1/mods/{id:guid}/notes", (Guid id, UpdateModNotesRequest request) =>
        {
            try
            {
                var mods = LoadLibrary();
                var mod = mods.FirstOrDefault(m => m.Id == id);
                if (mod is null)
                    return Results.NotFound($"Mod {id} not found.");

                mod.Notes = request.Notes;
                SaveLibrary(mods);
                return Results.Ok(ToDto(mod));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update notes: {ex.Message}");
            }
        });
    }

    private static List<InstalledMod> LoadLibrary()
    {
        if (!File.Exists(AppDataPaths.LibraryFile)) return [];
        var json = File.ReadAllText(AppDataPaths.LibraryFile);
        return JsonSerializer.Deserialize<List<InstalledMod>>(json, JsonOpts) ?? [];
    }

    private static void SaveLibrary(List<InstalledMod> mods)
    {
        var dir = Path.GetDirectoryName(AppDataPaths.LibraryFile)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(AppDataPaths.LibraryFile, JsonSerializer.Serialize(mods));
    }

    private static InstalledModDto ToDto(InstalledMod mod) =>
        new(mod.Id, mod.Name, mod.Type.ToString(), mod.TypeColor, mod.TypeLabel,
            mod.IsEnabled, mod.IsFavorite, mod.HasConflict,
            mod.Version, mod.Author,
            mod.InstalledAt.ToString("o"), mod.TotalSizeBytes, mod.TotalSizeDisplay,
            mod.DetectionScore, mod.Notes, mod.ImageUrl, mod.ThumbnailUrl,
            mod.LoadOrderPriority);
}
