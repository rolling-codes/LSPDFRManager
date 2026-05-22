using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class LibraryEndpoints
{
    // Shared file store — JsonFileStore<List<InstalledMod>> carries a static file-level lock
    // that is the same lock used by ModLibraryService, preventing torn writes across the
    // in-process WPF layer and the API layer.
    private static readonly JsonFileStore<List<InstalledMod>> Store =
        new(AppDataPaths.LibraryFile);

    // Endpoint-level guard for the read-modify-write cycle
    private static readonly SemaphoreSlim Mutex = new(1, 1);

    private static readonly InstalledModFileService FileService = new();

    public static void MapLibrary(this WebApplication app)
    {
        app.MapGet("/api/v1/mods", (string? search, string? enabled, string? type) =>
        {
            try
            {
                IEnumerable<InstalledMod> mods = Store.LoadOrDefault(static () => []);

                if (!string.IsNullOrWhiteSpace(search))
                    mods = mods.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(enabled) && bool.TryParse(enabled, out var isEnabled))
                    mods = mods.Where(m => m.IsEnabled == isEnabled);

                if (!string.IsNullOrWhiteSpace(type))
                    mods = mods.Where(m => m.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase));

                var dtos = mods.Select(ToDto).ToList();
                return Results.Ok(new ModsListResponse(dtos, dtos.Count));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to read library: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/mods/{id:guid}/enable", async (Guid id, ToggleModRequest request) =>
        {
            if (LocalApiHost.SetEnabledCallback is { } setEnabled)
            {
                // In-process mode: delegate to ModLibraryService so the WPF collection stays consistent.
                try
                {
                    setEnabled(id, request.Enabled);
                    var mods = Store.LoadOrDefault(static () => []);
                    var mod  = mods.FirstOrDefault(m => m.Id == id);
                    return mod is null ? Results.NotFound($"Mod {id} not found.") : Results.Ok(ToDto(mod));
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Failed to toggle mod: {ex.Message}");
                }
            }

            await Mutex.WaitAsync();
            try
            {
                var mods = Store.LoadOrDefault(static () => []);
                var mod  = mods.FirstOrDefault(m => m.Id == id);
                if (mod is null)
                    return Results.NotFound($"Mod {id} not found.");

                FileService.SetEnabled(mod, request.Enabled);
                Store.Save(mods);
                return Results.Ok(ToDto(mod));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to toggle mod: {ex.Message}");
            }
            finally
            {
                Mutex.Release();
            }
        });

        app.MapPut("/api/v1/mods/{id:guid}/notes", async (Guid id, UpdateModNotesRequest request) =>
        {
            if (LocalApiHost.UpdateNotesCallback is { } updateNotes)
            {
                // In-process mode: delegate to ModLibraryService so the WPF collection stays consistent.
                try
                {
                    updateNotes(id, request.Notes);
                    var mods = Store.LoadOrDefault(static () => []);
                    var mod  = mods.FirstOrDefault(m => m.Id == id);
                    return mod is null ? Results.NotFound($"Mod {id} not found.") : Results.Ok(ToDto(mod));
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Failed to update notes: {ex.Message}");
                }
            }

            await Mutex.WaitAsync();
            try
            {
                var mods = Store.LoadOrDefault(static () => []);
                var mod  = mods.FirstOrDefault(m => m.Id == id);
                if (mod is null)
                    return Results.NotFound($"Mod {id} not found.");

                mod.Notes = request.Notes ?? "";
                Store.Save(mods);
                return Results.Ok(ToDto(mod));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update notes: {ex.Message}");
            }
            finally
            {
                Mutex.Release();
            }
        });

        app.MapPost("/api/v1/mods/sync", () =>
        {
            try
            {
                int pruned;
                if (LocalApiHost.SyncLibraryCallback is { } sync)
                {
                    // In-process mode: delegate to ModLibraryService which owns the in-memory
                    // collection and its mutation lock — avoids a separate disk read-modify-write.
                    pruned = sync();
                }
                else
                {
                    // Standalone / dev-server mode: fall back to direct file read.
                    var mods = Store.LoadOrDefault(static () => []);
                    var before = mods.Count;
                    mods.RemoveAll(InstalledModFileService.IsOrphaned);
                    pruned = before - mods.Count;
                    if (pruned > 0)
                        Store.Save(mods);
                }
                return Results.Ok(new { pruned });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Sync failed: {ex.Message}");
            }
        });
    }

    private static InstalledModDto ToDto(InstalledMod mod) =>
        new(mod.Id, mod.Name, mod.Type.ToString(), mod.TypeColor, mod.TypeLabel,
            mod.IsEnabled, mod.IsFavorite, mod.HasConflict,
            mod.Version, mod.Author,
            mod.InstalledAt.ToString("o"), mod.TotalSizeBytes, mod.TotalSizeDisplay,
            mod.DetectionScore, mod.Notes, mod.ImageUrl, mod.ThumbnailUrl,
            mod.LoadOrderPriority, InstalledModFileService.IsOrphaned(mod));
}
