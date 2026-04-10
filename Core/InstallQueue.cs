using System.Collections.Concurrent;
using LSPDFRManager.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.Core;

/// <summary>
/// Background queue that processes mod installations one at a time.
/// </summary>
public class InstallQueue : IDisposable
{
    private readonly ConcurrentQueue<ModInfo> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public event Action<ModInfo>? InstallStarted;
    public event Action<InstalledMod>? InstallCompleted;
    public event Action<ModInfo, string>? InstallFailed;

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

                // Snapshot files before installation to detect what was added
                var filesBefore = Directory.Exists(gtaPath)
                    ? Directory.GetFiles(gtaPath, "*", SearchOption.AllDirectories)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : [];

                await Task.Run(() => FileInstaller.Install(mod, gtaPath)).ConfigureAwait(false);

                var filesAfter = Directory.Exists(gtaPath)
                    ? Directory.GetFiles(gtaPath, "*", SearchOption.AllDirectories)
                    : [];

                var newFiles = filesAfter
                    .Where(f => !filesBefore.Contains(f))
                    .ToList();

                // Fall back to manifest file list if diff is empty (e.g. overwrite installs)
                if (newFiles.Count == 0)
                    newFiles = mod.Files
                        .Select(f => Path.Combine(gtaPath, f.Replace('/', Path.DirectorySeparatorChar)))
                        .ToList();

                var installed = new InstalledMod
                {
                    Name        = mod.Name,
                    Type        = mod.Type,
                    TypeLabel   = mod.TypeLabel,
                    TypeColor   = mod.TypeColor,
                    Version     = mod.Version ?? "",
                    SourcePath  = mod.SourcePath,
                    InstallPath = gtaPath,
                    DlcPackName = mod.DlcPackName ?? "",
                    InstalledFiles = newFiles,
                };

                ModLibraryService.Instance.Add(installed);
                InstallCompleted?.Invoke(installed);
                AppLogger.Info($"Installed: {mod.Name}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Install failed for {mod.Name}", ex);
                InstallFailed?.Invoke(mod, ex.Message);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
