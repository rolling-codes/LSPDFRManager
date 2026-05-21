using System.Diagnostics;
using System.Net.Http;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class BrowseApiServiceManager : IDisposable
{
    private static BrowseApiServiceManager? _instance;
    public static BrowseApiServiceManager Instance => _instance ??= new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private Process? _process;
    private CancellationTokenSource? _cts;

    public BrowseApiStatus Status { get; private set; } = BrowseApiStatus.Offline;
    public string StatusMessage { get; private set; } = "Not started";
    public event Action? StatusChanged;

    private void SetStatus(BrowseApiStatus status, string message)
    {
        Status = status;
        StatusMessage = message;
        StatusChanged?.Invoke();
        AppLogger.Info($"[BrowseAPI] {status}: {message}");
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            var baseUrl = AppConfig.Instance.BrowseApiBaseUrl;
            var response = await _http.GetAsync($"{baseUrl}/health").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (Status == BrowseApiStatus.Online || Status == BrowseApiStatus.Starting)
            return;

        if (await IsOnlineAsync().ConfigureAwait(false))
        {
            SetStatus(BrowseApiStatus.Online, "API is already running on another process.");
            return;
        }

        var exePath = FindApiExecutable();
        if (exePath is null)
        {
            SetStatus(BrowseApiStatus.MissingExecutable, "LSPDFRManager.Api.exe not found.");
            return;
        }

        SetStatus(BrowseApiStatus.Starting, "Starting Browse API service…");

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _process = new Process
            {
                StartInfo = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            _process.Start();

            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500, _cts.Token).ConfigureAwait(false);
                if (await IsOnlineAsync().ConfigureAwait(false))
                {
                    SetStatus(BrowseApiStatus.Online, "Browse API is online.");
                    Log("Service started.");
                    return;
                }
            }

            SetStatus(BrowseApiStatus.Error, "API started but did not respond within 5 seconds.");
        }
        catch (OperationCanceledException)
        {
            SetStatus(BrowseApiStatus.Offline, "Start cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus(BrowseApiStatus.Error, $"Failed to start: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _process?.Kill(); } catch { }
        _process = null;
        SetStatus(BrowseApiStatus.Offline, "Stopped.");
        Log("Service stopped.");
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        Stop();
        await Task.Delay(500, ct).ConfigureAwait(false);
        await StartAsync(ct).ConfigureAwait(false);
    }

    public async Task CheckStatusAsync()
    {
        if (await IsOnlineAsync().ConfigureAwait(false))
            SetStatus(BrowseApiStatus.Online, "API is online.");
        else
            SetStatus(BrowseApiStatus.Offline, "API is not responding.");
    }

    private static string? FindApiExecutable()
    {
        var configured = AppConfig.Instance.BrowseApiPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "LSPDFRManager.Api.exe"),
            Path.Combine(appDir, "Api", "LSPDFRManager.Api.exe"),
            Path.Combine(appDir, "LSPDFRManager.Api", "LSPDFRManager.Api.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void Log(string message)
    {
        try
        {
            var logPath = AppDataPaths.BrowseApiLogFile;
            var dir = Path.GetDirectoryName(logPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
        _cts?.Dispose();
    }
}
