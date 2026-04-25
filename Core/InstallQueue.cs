using System.Collections.Concurrent;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.Core;

/// <summary>
/// Background queue that processes mod installations one at a time.
/// </summary>
public class InstallQueue : IDisposable
{
    private static readonly Lazy<InstallQueue> _instance = new(() => new InstallQueue());
    public static InstallQueue Instance => _instance.Value;

    private readonly ConcurrentQueue<ModInfo> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public event Action<ModInfo>? InstallStarted;
    public event Action<InstalledMod>? InstallCompleted;
    public event Action<ModInfo, string>? InstallFailed;
    public event Action<ModInfo, InstallResult>? InstallFailedWithResult;

    public InstallQueue() => _worker = Task.Run(ProcessLoop);

    public void Enqueue(ModInfo mod)
    {
        _queue.Enqueue(mod);
        _signal.Release();
    }

    private async Task ProcessLoop()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            if (!_queue.TryDequeue(out var mod)) continue;

            InstallStarted?.Invoke(mod);
            try
            {
                var gtaPath = AppConfig.Instance.GtaPath;

                var result = await FileInstaller.InstallAsync(mod, gtaPath).ConfigureAwait(false);

                if (!result.Success)
                {
                    AppLogger.Error($"Install failed for {mod.Name}: {result.Error}");
                    InstallFailed?.Invoke(mod, result.Error ?? "Unknown error");
                    InstallFailedWithResult?.Invoke(mod, result);
                    continue;
                }

                // Register what was written (or fall back to manifest)
                var newFiles = result.FilesWritten > 0
                    ? Directory.GetFiles(gtaPath, "*", SearchOption.AllDirectories)
                        .Where(f => File.GetCreationTimeUtc(f) >= DateTime.UtcNow.AddSeconds(-5))
                        .ToList()
                    : mod.Files
                        .Select(f => Path.Combine(gtaPath, f.Replace('/', Path.DirectorySeparatorChar)))
                        .ToList();

                var installed = new InstalledMod
                {
                    Name        = mod.Name,
                    Type        = mod.Type,
                    TypeLabel   = mod.TypeLabel,
                    TypeColor   = mod.TypeColor,
                    Version     = mod.Version ?? "",
                    Author      = mod.Author ?? "",
                    SourcePath  = mod.SourcePath,
                    InstallPath = gtaPath,
                    DlcPackName = mod.DlcPackName ?? "",
                    InstalledFiles = newFiles,
                };

                ModLibraryService.Instance.Add(installed);

                // Register vehicle DLC packs in dlclist.xml so GTA V loads them
                if (installed.Type == ModType.VehicleDlc && !string.IsNullOrEmpty(installed.DlcPackName))
                    DlcListService.AddEntry(installed.DlcPackName);

                InstallCompleted?.Invoke(installed);
                AppLogger.Info($"Installed: {mod.Name} ({result.FilesWritten} files)");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Install queue error for {mod.Name}", ex);
                InstallFailed?.Invoke(mod, ex.Message);
                var result = new InstallResult { Success = false, Error = ex.Message };
                InstallFailedWithResult?.Invoke(mod, result);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
