using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class CompatibilityEndpoints
{
    private static readonly VersionDetectorService Detector = new();

    public static void MapCompatibility(this WebApplication app)
    {
        app.MapGet("/api/v1/compatibility", async () =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            var configured = !string.IsNullOrWhiteSpace(gtaPath) && Directory.Exists(gtaPath);

            if (!configured)
            {
                var empty = new List<ComponentVersionDto>
                {
                    new("GTA5",          false, null, null),
                    new("LSPDFR",        false, null, null),
                    new("RagePluginHook",false, null, null),
                    new("ScriptHookV",   false, null, null),
                    new("ScriptHookVDotNet", false, null, null),
                };
                return Results.Ok(new CompatibilityResponse(empty, false, DateTime.UtcNow));
            }

            try
            {
                var bundle = await Detector.DetectAsync(gtaPath);

                var components = new List<ComponentVersionDto>
                {
                    new("GTA5",          bundle.GtaPresent,            bundle.GtaVersion,                 bundle.GtaHash),
                    new("LSPDFR",        bundle.LspdfrPresent,         bundle.LspdfrVersion,              bundle.LspdfrHash),
                    new("RagePluginHook",bundle.RagePluginHookPresent, bundle.RagePluginHookVersion,      bundle.RagePluginHookHash),
                    new("ScriptHookV",   bundle.ScriptHookVVersion is not null, bundle.ScriptHookVVersion, bundle.ScriptHookVHash),
                    new("ScriptHookVDotNet", bundle.ScriptHookVDotNetVersion is not null, bundle.ScriptHookVDotNetVersion, bundle.ScriptHookVDotNetHash),
                };

                return Results.Ok(new CompatibilityResponse(components, true, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to detect component versions: {ex.Message}");
            }
        });
    }
}
