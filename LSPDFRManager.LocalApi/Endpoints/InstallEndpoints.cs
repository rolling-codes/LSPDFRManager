using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.LocalApi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class InstallEndpoints
{
    public static void MapInstall(this WebApplication app)
    {
        app.MapPost("/api/v1/install", (StartInstallRequest request, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath))
                return Results.BadRequest("SourcePath is required.");

            if (request.SourcePath.Contains(".."))
                return Results.BadRequest("SourcePath must not contain path traversal sequences.");

            var gtaPath = AppConfig.Instance.GtaPath;
            if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
                return Results.BadRequest("GTA V path is not configured or does not exist.");

            var sourceExists = File.Exists(request.SourcePath) || Directory.Exists(request.SourcePath);
            if (!sourceExists)
                return Results.BadRequest($"Source path does not exist: {request.SourcePath}");

            var callback = LocalApiHost.ExecuteInstallCallback;
            if (callback is null)
                return Results.Problem("Install service is not available in standalone mode.");

            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var jobId = queue.CreateJob();
            var sourcePath = request.SourcePath;

            _ = Task.Run(async () =>
            {
                try
                {
                    queue.UpdateProgress(jobId, 10, "Detecting mod");

                    var modInfo = new ModInfo
                    {
                        SourcePath = sourcePath,
                        Name = Path.GetFileNameWithoutExtension(sourcePath),
                    };

                    queue.UpdateProgress(jobId, 30, "Queuing install");

                    var result = await callback(modInfo);

                    if (result.Success)
                        queue.CompleteJob(jobId);
                    else
                        queue.FailJob(jobId, result.UserMessage ?? result.Error);
                }
                catch (Exception ex)
                {
                    queue.FailJob(jobId, ex.Message);
                }
            });

            return Results.Ok(new StartInstallResponse(jobId));
        });
    }
}
