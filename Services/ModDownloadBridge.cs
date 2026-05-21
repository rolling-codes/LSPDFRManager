using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Install;

namespace LSPDFRManager.Services;

/// <summary>
/// Bridge between the WebView2 download pipeline and install review staging.
/// It detects downloaded archives, but does not enqueue or install them.
/// </summary>
public class ModDownloadBridge
{
    private static readonly Lazy<ModDownloadBridge> _lazy = new(() => new());
    public static ModDownloadBridge Instance => _lazy.Value;

    private readonly IInstallController _installController;

    public ModDownloadBridge(IInstallController? installController = null)
    {
        _installController = installController ?? new InstallWorkflowController();
    }

    /// <summary>Raised on the UI thread when a mod has been detected and staged for review.</summary>
    public event Action<ModInfo>? Staged;

    /// <summary>Raised on the UI thread when detection fails.</summary>
    public event Action<string, string>? Failed; // (fileName, errorMessage)

    /// <summary>Raised on the UI thread while detection is running.</summary>
    public event Action<string>? Detecting; // (fileName)

    /// <summary>
    /// Called by the Browse code-behind once WebView2 has finished writing the
    /// download to <paramref name="localPath"/>.
    /// </summary>
    public void OnDownloadCompleted(string localPath, string displayName)
    {
        if (string.IsNullOrWhiteSpace(localPath)) return;
        _ = StageDownloadAsync(localPath, displayName);
    }

    public async Task StageDownloadAsync(string localPath, string displayName)
    {
        if (string.IsNullOrWhiteSpace(localPath)) return;

        UiDispatcher.Invoke(() => Detecting?.Invoke(displayName));

        try
        {
            var mod = await _installController.StageBrowseDownloadAsync(localPath, displayName).ConfigureAwait(false);

            UiDispatcher.Invoke(() => Staged?.Invoke(mod));
            AppLogger.Info($"[BROWSE_BRIDGE] Staged '{mod.Name}' ({mod.TypeLabel}) from {displayName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[BROWSE_BRIDGE] Detection failed for {displayName}", ex);
            UiDispatcher.Invoke(() => Failed?.Invoke(displayName, ex.Message));
        }
    }
}
