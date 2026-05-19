using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class PatrolReadinessEndpoints
{
    private static readonly VersionDetectorService Detector = new();

    public static void MapPatrolReadiness(this WebApplication app)
    {
        app.MapGet("/api/v1/patrol-readiness", async () =>
        {
            var blocking = new List<string>();
            var warnings = new List<string>();
            var passing  = new List<string>();

            var gtaPath = AppConfig.Instance.GtaPath;

            if (string.IsNullOrWhiteSpace(gtaPath))
            {
                blocking.Add("GTA V path is not configured. Open Settings to set it.");
                return Results.Ok(Build(blocking, warnings, passing));
            }

            if (!Directory.Exists(gtaPath))
            {
                blocking.Add($"GTA V directory does not exist: {gtaPath}");
                return Results.Ok(Build(blocking, warnings, passing));
            }

            passing.Add("GTA V path is configured and the directory exists.");

            VersionBundle? bundle = null;
            try
            {
                bundle = await Detector.DetectAsync(gtaPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                warnings.Add($"Version scan could not complete: {ex.Message}");
            }

            if (bundle is null || !bundle.GtaPresent)
                blocking.Add("GTA5.exe was not found in the configured GTA V directory.");
            else
                passing.Add("GTA5.exe found.");

            if (bundle is null || !bundle.RagePluginHookPresent)
                blocking.Add("RAGEPluginHook.exe is missing — LSPDFR cannot launch.");
            else
                passing.Add("RAGEPluginHook.exe found.");

            if (bundle is null || !bundle.LspdfrPresent)
                blocking.Add("LSPDFR core files were not found.");
            else
                passing.Add("LSPDFR core files found.");

            if (bundle is not null)
            {
                if (bundle.ScriptHookVVersion is null)
                    warnings.Add("ScriptHookV.dll not found — some mods may not work.");
                else
                    passing.Add("ScriptHookV.dll found.");

                if (bundle.ScriptHookVDotNetVersion is null)
                    warnings.Add("ScriptHookVDotNet not found — CS/VB script mods will not load.");
                else
                    passing.Add("ScriptHookVDotNet found.");

                passing.Add("Version scan completed.");
            }

            return Results.Ok(Build(blocking, warnings, passing));
        });
    }

    private static PatrolReadinessResultDto Build(
        List<string> blocking,
        List<string> warnings,
        List<string> passing) =>
        new(blocking, warnings, passing, blocking.Count == 0);
}
