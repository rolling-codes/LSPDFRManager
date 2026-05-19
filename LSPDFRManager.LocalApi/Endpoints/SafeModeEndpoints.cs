using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

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

                // Count actions that will be disabled
                var willDisableCount = plan.Actions.Count(a => a.WillDisable);

                return Results.Ok(new SafeModeApplyResponse(
                    Success: true,
                    Error: null,
                    FilesDisabled: willDisableCount));
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

            try
            {
                // Re-enable .disabled files in plugins/lspdfr and root .asi files
                var restoredCount = 0;
                var searchDirs = new[]
                {
                    gtaPath,
                    Path.Combine(gtaPath, "plugins", "lspdfr"),
                    Path.Combine(gtaPath, "scripts"),
                };

                foreach (var dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.EnumerateFiles(dir, "*.disabled", SearchOption.AllDirectories))
                    {
                        var original = file[..^".disabled".Length];
                        if (!File.Exists(original))
                        {
                            File.Move(file, original);
                            restoredCount++;
                        }
                    }
                }

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
