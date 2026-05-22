using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

file record SafeModeManifest(IReadOnlyList<string> DisabledPaths);

public static class SafeModeEndpoints
{
    // Valid modes accepted by EmergencyRecoveryService.BuildPlan
    private static readonly string[] ValidModes =
    [
        "DisableAllOptionalPlugins",
        "DisableAllAsiExceptRequired",
        "DisableScriptsFolder",
    ];

    public static void MapSafeMode(this WebApplication app)
    {
        app.MapGet("/api/v1/safe-mode/plan", (string? mode) =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            var resolvedMode = mode ?? "DisableAllOptionalPlugins";
            if (!ValidModes.Contains(resolvedMode, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest($"Invalid mode. Valid modes: {string.Join(", ", ValidModes)}");

            try
            {
                var service = new EmergencyRecoveryService();
                var plan = service.BuildPlan(resolvedMode);

                var dto = new EmergencyRecoveryPlanDto(
                    Mode: plan.Mode,
                    Actions: plan.Actions.Select(a => new EmergencyRecoveryActionDto(
                        Description: a.Description,
                        AffectedPath: a.AffectedPath,
                        WillDisable: a.WillDisable)).ToList(),
                    CreatedAt: plan.CreatedAt);

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to build safe mode plan: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/safe-mode/apply", async (EmergencyRecoveryPlanDto? planDto, CancellationToken ct) =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            var resolvedMode = planDto?.Mode ?? "DisableAllOptionalPlugins";
            if (!ValidModes.Contains(resolvedMode, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest($"Invalid mode. Valid modes: {string.Join(", ", ValidModes)}");

            try
            {
                var service = new EmergencyRecoveryService();
                var plan = service.BuildPlan(resolvedMode);

                var disabledCount = 0;
                var progress = new Progress<string>(_ => disabledCount++);
                await service.ApplyAsync(plan, progress);

                // Collect files that were actually renamed to .disabled
                var actuallyDisabled = plan.Actions
                    .Where(a => a.WillDisable)
                    .Select(a => a.AffectedPath + ".disabled")
                    .Where(File.Exists)
                    .ToList();

                // Write manifest so restore only re-enables what we actually disabled
                var manifestPath = Path.Combine(AppDataPaths.Root, "safe_mode_state.json");
                var manifest = new SafeModeManifest(actuallyDisabled);
                File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

                return Results.Ok(new SafeModeApplyResponse(
                    Success: true,
                    Error: null,
                    FilesDisabled: actuallyDisabled.Count));
            }
            catch (Exception ex)
            {
                return Results.Ok(new SafeModeApplyResponse(
                    Success: false,
                    Error: ex.Message,
                    FilesDisabled: 0));
            }
        });

        app.MapPost("/api/v1/safe-mode/restore", async (CancellationToken ct) =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            var manifestPath = Path.Combine(AppDataPaths.Root, "safe_mode_state.json");
            if (!File.Exists(manifestPath))
                return Results.BadRequest("No active safe mode session found. Nothing to restore.");

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<SafeModeManifest>(json);
                if (manifest is null)
                    return Results.BadRequest("Safe mode manifest is corrupt. Nothing to restore.");

                var restoredCount = 0;
                foreach (var disabledPath in manifest.DisabledPaths)
                {
                    if (!disabledPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(disabledPath)) continue;
                    var original = disabledPath[..^".disabled".Length];
                    if (!File.Exists(original))
                    {
                        File.Move(disabledPath, original);
                        restoredCount++;
                    }
                }

                File.Delete(manifestPath);

                return Results.Ok(new SafeModeApplyResponse(
                    Success: true,
                    Error: null,
                    FilesDisabled: restoredCount));
            }
            catch (Exception ex)
            {
                return Results.Ok(new SafeModeApplyResponse(
                    Success: false,
                    Error: ex.Message,
                    FilesDisabled: 0));
            }
        });
    }
}
