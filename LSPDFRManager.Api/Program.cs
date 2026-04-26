using LSPDFRManager.Api.Models;
using LSPDFRManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<LcpdfrScraper>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
});

var app = builder.Build();

// ── Health check ────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0" }));

// ── Mod search ──────────────────────────────────────────────────────────────
app.MapGet("/api/mods/search", async (string q, string? category,
    LcpdfrScraper scraper, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "q is required" });

    var results = await scraper.SearchAsync(q, category, ct);
    return Results.Ok(results);
});

// ── Mod detail ──────────────────────────────────────────────────────────────
app.MapGet("/api/mods/{id}", async (string id, LcpdfrScraper scraper, CancellationToken ct) =>
{
    var mod = await scraper.GetModAsync(id, ct);
    return mod is null ? Results.NotFound() : Results.Ok(mod);
});

app.Run();
