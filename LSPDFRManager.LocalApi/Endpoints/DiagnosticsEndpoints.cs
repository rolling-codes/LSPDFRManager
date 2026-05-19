using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class DiagnosticsEndpoints
{
    public static void MapDiagnostics(this WebApplication app)
    {
        app.MapGet("/api/v1/diagnostics", async (CancellationToken ct) =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
            {
                var notConfigured = new DiagnosticFindingDto(
                    Category: "Config",
                    Title: "GTA V path not configured",
                    Detail: null,
                    RecommendedFix: "Set GTA V path in Settings",
                    AffectedPath: null,
                    Severity: "Error");

                return Results.Ok(new DiagnosticsResponse(
                    Findings: [notConfigured],
                    TotalFindings: 1,
                    ErrorCount: 1,
                    WarningCount: 0));
            }

            try
            {
                var orchestrator = new DiagnosticsOrchestrator();
                var findings = await orchestrator.RunAllAsync(null, ct);

                var dtos = findings.Select(f => new DiagnosticFindingDto(
                    Category: f.Category,
                    Title: f.Title,
                    Detail: string.IsNullOrEmpty(f.Detail) ? null : f.Detail,
                    RecommendedFix: f.RecommendedFix,
                    AffectedPath: f.AffectedPath,
                    Severity: f.Severity.ToString()
                )).ToList();

                return Results.Ok(new DiagnosticsResponse(
                    Findings: dtos,
                    TotalFindings: dtos.Count,
                    ErrorCount: dtos.Count(d => d.Severity is "Error" or "Critical"),
                    WarningCount: dtos.Count(d => d.Severity == "Warning")));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Diagnostics scan failed: {ex.Message}");
            }
        });
    }
}
