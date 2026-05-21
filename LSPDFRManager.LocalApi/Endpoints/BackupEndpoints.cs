using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.LocalApi.Services;
using LSPDFRManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class BackupEndpoints
{
    public static void MapBackups(this WebApplication app)
    {
        app.MapGet("/api/v1/backups", () =>
        {
            var backupPath = AppConfig.Instance.BackupPath;

            if (string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
                return Results.Ok(new BackupsListResponse([]));

            try
            {
                var files = Directory.GetFiles(backupPath, "*.zip")
                    .Select(path =>
                    {
                        var info = new FileInfo(path);
                        return new BackupFileDto(
                            FileName: info.Name,
                            FilePath: info.FullName,
                            SizeBytes: info.Length,
                            SizeDisplay: FormatSize(info.Length),
                            LastWriteUtc: info.LastWriteTimeUtc);
                    })
                    .OrderByDescending(f => f.LastWriteUtc)
                    .ToList();

                return Results.Ok(new BackupsListResponse(files));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to list backups: {ex.Message}");
            }
        });

        app.MapPost("/api/v1/backups", (HttpContext ctx) =>
        {
            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var jobId = queue.CreateJob();

            _ = Task.Run(async () =>
            {
                queue.UpdateProgress(jobId, 0, "Running");
                try
                {
                    var svc = new BackupService();
                    var progress = new Progress<string>(_ => { });
                    await svc.CreateBackupAsync(progress);
                    queue.CompleteJob(jobId);
                }
                catch (Exception ex)
                {
                    queue.FailJob(jobId, ex.Message);
                }
            });

            return Results.Ok(new CreateBackupResponse(jobId));
        });

        app.MapPost("/api/v1/backups/restore", async (RestoreBackupRequest request, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return Results.BadRequest("FileName is required.");

            var backupPath = AppConfig.Instance.BackupPath;
            if (string.IsNullOrWhiteSpace(backupPath))
                return Results.BadRequest("Backup path is not configured.");

            var resolvedPath = Path.GetFullPath(Path.Combine(backupPath, request.FileName));
            var resolvedRoot = Path.GetFullPath(backupPath);

            if (!resolvedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Invalid file name.");

            if (!resolvedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Only .zip backups can be restored.");

            if (!File.Exists(resolvedPath))
                return Results.NotFound($"Backup '{request.FileName}' not found.");

            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var jobId = queue.CreateJob();

            _ = Task.Run(async () =>
            {
                queue.UpdateProgress(jobId, 0, "Running");
                try
                {
                    var svc = new BackupService();
                    var progress = new Progress<string>(_ => { });
                    await svc.RestoreFromBackupAsync(resolvedPath, progress);
                    queue.CompleteJob(jobId);
                }
                catch (Exception ex)
                {
                    queue.FailJob(jobId, ex.Message);
                }
            });

            return Results.Ok(new RestoreBackupResponse(jobId));
        });

        app.MapDelete("/api/v1/backups/{fileName}", (string fileName, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return Results.BadRequest("fileName is required.");

            var backupPath = AppConfig.Instance.BackupPath;
            if (string.IsNullOrWhiteSpace(backupPath))
                return Results.BadRequest("Backup path is not configured.");

            var resolvedPath = Path.GetFullPath(Path.Combine(backupPath, fileName));
            var resolvedRoot = Path.GetFullPath(backupPath);

            if (!resolvedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Invalid file name.");

            if (!resolvedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Only .zip backups can be deleted.");

            if (!File.Exists(resolvedPath))
                return Results.NotFound($"Backup '{fileName}' not found.");

            try
            {
                File.Delete(resolvedPath);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete backup: {ex.Message}");
            }
        });

        app.MapGet("/api/v1/jobs/{jobId}", (string jobId, HttpContext ctx) =>
        {
            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var job = queue.GetJob(jobId);
            if (job is null)
                return Results.NotFound($"Job '{jobId}' not found.");

            return Results.Ok(new JobStatusDto(job.JobId, job.State, job.ProgressPct, job.Error, job.ResultJson));
        });
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
