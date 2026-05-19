using System.Net;
using System.Net.Sockets;
using LSPDFRManager.LocalApi.Endpoints;
using LSPDFRManager.LocalApi.Middleware;

namespace LSPDFRManager.LocalApi;

/// <summary>
/// Hosts LSPDFRManager.LocalApi in-process inside the WPF shell.
/// Picks a free port at startup and signals <see cref="PortTask"/> once listening.
/// </summary>
public static class LocalApiHost
{
    private static readonly TaskCompletionSource<int> _portReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private static WebApplication? _app;

    /// <summary>Awaitable port number — completes once the server is listening.</summary>
    public static Task<int> PortTask => _portReady.Task;

    public static int Port => _portReady.Task.IsCompletedSuccessfully
        ? _portReady.Task.Result : 0;

    public static string BaseUrl => Port > 0
        ? $"http://127.0.0.1:{Port}" : string.Empty;

    public static async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var port = FindFreePort();

        var options = new WebApplicationOptions
        {
            // ContentRootPath is the WPF exe directory at runtime; wwwroot/ is copied there.
            ContentRootPath = AppContext.BaseDirectory,
        };

        var builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        _app = app;

        app.UseMiddleware<LocalhostOnlyMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0" }));
        app.MapHistory();
        app.MapLogs();
        app.MapCompatibility();
        app.MapConfig();
        app.MapPatrolReadiness();
        app.MapBrowse();
        app.MapFallbackToFile("index.html");

        await app.StartAsync(cancellationToken);

        _portReady.TrySetResult(port);
    }

    public static async Task StopAsync()
    {
        if (_app is not null)
            await _app.StopAsync();
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
