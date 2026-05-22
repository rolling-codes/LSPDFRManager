using LSPDFRManager.LocalApi.Endpoints;
using LSPDFRManager.LocalApi.Middleware;

using LSPDFRManager.LocalApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JobQueue>();

var app = builder.Build();

app.UseMiddleware<LocalhostOnlyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status  = "ok",
    version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
}));
app.MapHistory();
app.MapLogs();
app.MapCompatibility();
app.MapConfig();
app.MapProfiles();
app.MapPatrolReadiness();
app.MapBackups();
app.MapJobs();
app.MapBrowse();
app.MapLibrary();
app.MapInstall();
app.MapCleanup();
app.MapDiagnostics();
app.MapSafeMode();
app.MapFallbackToFile("index.html");

app.Run("http://127.0.0.1:5284");

public partial class Program { }
