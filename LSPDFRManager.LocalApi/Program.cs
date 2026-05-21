using LSPDFRManager.LocalApi.Endpoints;
using LSPDFRManager.LocalApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseMiddleware<LocalhostOnlyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0" }));
app.MapHistory();
app.MapLogs();
app.MapCompatibility();
app.MapConfig();
app.MapLibrary();
app.MapProfiles();
app.MapPatrolReadiness();
app.MapBrowse();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
