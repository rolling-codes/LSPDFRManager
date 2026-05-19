using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class CleanupEndpoints
{
    public static void MapCleanup(this WebApplication app)
    {
        app.MapGet("/api/v1/cleanup/scan", () =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            try
            {
                var result = LspdfrCleanupScanner.Scan(gtaPath);
                var candidates = result.AllCandidates.Select(ToDto).ToList();
                var totalSize = candidates.Sum(c => c.SizeBytes);
                return Results.Ok(new CleanupScanResponse(candidates, totalSize));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Cleanup scan failed: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/cleanup/apply", async (CleanupApplyRequest req, CancellationToken ct) =>
        {
            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            // Parse mode: accept "Safe"/"Aggressive" aliases or direct enum names
            CleanupMode mode;
            if (string.Equals(req.Mode, "Safe", StringComparison.OrdinalIgnoreCase))
                mode = CleanupMode.SafeCoreReset;
            else if (string.Equals(req.Mode, "Aggressive", StringComparison.OrdinalIgnoreCase))
                mode = CleanupMode.FullEcosystemCleanout;
            else if (!Enum.TryParse<CleanupMode>(req.Mode, ignoreCase: true, out mode))
                return Results.BadRequest($"Invalid mode '{req.Mode}'. Valid values: Safe, Aggressive.");

            // Re-scan to get full RemovalCandidate objects
            CleanupScanResult scanResult;
            try
            {
                scanResult = LspdfrCleanupScanner.Scan(gtaPath);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Re-scan failed: {ex.Message}");
            }

            var pathSet = req.RelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selected = scanResult.AllCandidates
                .Where(c => pathSet.Contains(c.RelativePath))
                .ToList();

            // Reject blocked candidates
            var blocked = selected.Where(c => c.Classification == CandidateClassification.Blocked).ToList();
            if (blocked.Count > 0)
            {
                var names = string.Join(", ", blocked.Select(b => b.RelativePath));
                return Results.BadRequest($"Cannot remove blocked candidates: {names}");
            }

            try
            {
                var service = new CleanupApplyService();
                var applyResult = await service.ApplyAsync(gtaPath, selected, mode, ct);

                return Results.Ok(new CleanupApplyResponse(
                    Success: applyResult.Success,
                    FilesDeleted: applyResult.DeletedPaths.Count,
                    BytesFreed: 0L, // size not tracked at deletion time
                    Error: applyResult.AbortReason));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Cleanup apply failed: {ex.Message}");
            }
        });
    }

    private static RemovalCandidateDto ToDto(RemovalCandidate c)
    {
        var sizeBytes = c.SizeBytes ?? ComputeSize(c);
        return new RemovalCandidateDto(
            RelativePath: c.RelativePath,
            Classification: c.Classification.ToString(),
            RiskLevel: c.RiskLevel.ToString(),
            SizeBytes: sizeBytes,
            SizeDisplay: FormatSize(sizeBytes),
            IsBlocked: c.Classification == CandidateClassification.Blocked);
    }

    private static long ComputeSize(RemovalCandidate c)
    {
        try
        {
            if (c.IsDirectory && Directory.Exists(c.FullPath))
                return new DirectoryInfo(c.FullPath)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            if (!c.IsDirectory && File.Exists(c.FullPath))
                return new FileInfo(c.FullPath).Length;
        }
        catch { /* best-effort */ }
        return 0L;
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
        };
    }
}
