using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.LocalApi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class JobEndpoints
{
    public static void MapJobs(this WebApplication app)
    {
        app.MapGet("/api/v1/jobs/{jobId}", (string jobId, HttpContext ctx) =>
        {
            var queue = ctx.RequestServices.GetRequiredService<JobQueue>();
            var job = queue.GetJob(jobId);
            if (job is null)
                return Results.NotFound($"Job '{jobId}' not found.");

            return Results.Ok(new JobStatusDto(job.JobId, job.State, job.ProgressPct, job.Error, job.ResultJson));
        });
    }
}
