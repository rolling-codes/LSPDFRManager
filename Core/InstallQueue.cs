using System.Collections.Concurrent;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.Core;

public class InstallQueue : IDisposable
{
    private static readonly Lazy<InstallQueue> LazyInstance = new(static () => new InstallQueue());
    public static InstallQueue Instance => LazyInstance.Value;

    private readonly ConcurrentQueue<QueuedInstall> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public event Action<ModInfo>? InstallStarted;
    public event Action<InstalledMod>? InstallCompleted;
    public event Action<ModInfo, string>? InstallFailed;
    public event Action<ModInfo, InstallResult>? InstallFailedWithResult;

    public InstallQueue() => _worker = Task.Run(ProcessLoopAsync);

    public void Enqueue(ModInfo mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        _queue.Enqueue(new QueuedInstall(mod));
        _signal.Release();
    }

    public Task<InstallResult> EnqueueAsync(ModInfo mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        var queued = new QueuedInstall(mod);
        _queue.Enqueue(queued);
        _signal.Release();

        return queued.Completion.Task;
    }

    private async Task ProcessLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_queue.TryDequeue(out var queued))
                continue;

            await ProcessInstallAsync(queued).ConfigureAwait(false);
        }
    }

    private async Task ProcessInstallAsync(QueuedInstall queued)
    {
        var mod = queued.Mod;

        try
        {
            InstallStarted?.Invoke(mod);

            var gtaPath = AppConfig.Instance.GtaPath;
            AppLogger.Info($"[INSTALL_START] {mod.Name} | source={mod.SourcePath} | target={gtaPath}");

            var result = await FileInstaller.InstallAsync(mod, gtaPath).ConfigureAwait(false);
            if (!result.Success)
            {
                HandleFailedInstall(mod, result);
                queued.Completion.TrySetResult(result);
                return;
            }

            var installed = CreateInstalledMod(mod, gtaPath, result);
            ModLibraryService.Instance.Add(installed);

            if (installed.Type == ModType.VehicleDlc && !string.IsNullOrWhiteSpace(installed.DlcPackName))
                DlcListService.AddEntry(installed.DlcPackName);

            InstallCompleted?.Invoke(installed);
            AppLogger.Info($"[INSTALL_SUCCESS] {mod.Name} | filesWritten={result.FilesWritten} | type={mod.Type}");

            queued.Completion.TrySetResult(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[INSTALL_EXCEPTION] {mod.Name}", ex);

            var result = new InstallResult
            {
                Success = false,
                Error = ex.Message,
            };

            InstallFailed?.Invoke(mod, ex.Message);
            InstallFailedWithResult?.Invoke(mod, result);
            queued.Completion.TrySetResult(result);
        }
    }

    private static InstalledMod CreateInstalledMod(ModInfo mod, string gtaPath, InstallResult result)
    {
        var installedFiles = result.WrittenFiles.Count > 0
            ? result.WrittenFiles
            : mod.Files
                .Select(file => Path.Combine(gtaPath, file.Replace('/', Path.DirectorySeparatorChar)))
                .ToList();

        return new InstalledMod
        {
            Name = mod.Name,
            Type = mod.Type,
            TypeLabel = mod.TypeLabel,
            TypeColor = mod.TypeColor,
            Version = mod.Version ?? "",
            Author = mod.Author ?? "",
            SourcePath = mod.SourcePath,
            InstallPath = gtaPath,
            DlcPackName = mod.DlcPackName ?? "",
            InstalledFiles = installedFiles,
            DetectionScore = (int)Math.Round(mod.Confidence * 100),
        };
    }

    private void HandleFailedInstall(ModInfo mod, InstallResult result)
    {
        AppLogger.Error(
            $"[INSTALL_FAILED] {mod.Name} | partial={result.IsPartial} | filesWritten={result.FilesWritten}");

        InstallFailed?.Invoke(mod, result.Error ?? "Unknown error");
        InstallFailedWithResult?.Invoke(mod, result);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Dispose();
        _cts.Dispose();
    }

    private sealed class QueuedInstall
    {
        public QueuedInstall(ModInfo mod) => Mod = mod;

        public ModInfo Mod { get; }
        public TaskCompletionSource<InstallResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
