using System.Text.Json;
using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.LocalApi.Services;
using LSPDFRManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class InstallEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var jobId = queue.CreateJob();

            var sourcePath = request.SourcePath;
            var targetRoot = gtaPath;

            _ = Task.Run(async () =>
            {
                queue.UpdateProgress(jobId, 0, "Running");
                try
                {
                    var mod = new ModInfo
                    {
                        SourcePath = sourcePath,
                        Name = Path.GetFileNameWithoutExtension(sourcePath),
                    };

                    var result = await FileInstaller.InstallAsync(mod, targetRoot);

                    if (result.Success)
                    {
                        var dto = new InstallResultDto(true, null, null, result.FilesWritten);
                        var json = JsonSerializer.Serialize(dto, JsonOpts);
                        queue.CompleteJob(jobId, json);
                    }
                    else
                    {
                        queue.FailJob(jobId, result.UserMessage ?? result.Error);
                    }
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
