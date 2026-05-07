using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Bridge between the WebView2 download pipeline and the install queue.
/// Owns detection and queuing so neither the view nor the VM needs to know
/// about ModDetector or InstallQueue directly.
/// </summary>
public class ModDownloadBridge
{
    private static readonly Lazy<ModDownloadBridge> _lazy = new(() => new());
    public static ModDownloadBridge Instance => _lazy.Value;

    private readonly ModDetector _detector = new();
    private readonly InstallQueue _queue = InstallQueue.Instance;

    /// <summary>Raised on the UI thread when a mod has been detected and queued.</summary>
    public event Action<ModInfo>? Queued;

    /// <summary>Raised on the UI thread when detection or queuing fails.</summary>
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
        _ = DetectAndEnqueueAsync(localPath, displayName);
    }

    private async Task DetectAndEnqueueAsync(string localPath, string displayName)
    {
        UiDispatcher.Invoke(() => Detecting?.Invoke(displayName));

        try
        {
            var mod = await Task.Run(() => _detector.Detect(localPath)).ConfigureAwait(false);

            // Use the display name from the browser as the mod name if detection
            // produced something generic
            if (string.IsNullOrWhiteSpace(mod.Name) || mod.Name.StartsWith("Mod "))
                mod.Name = CleanDisplayName(displayName);

            _queue.Enqueue(mod);

            UiDispatcher.Invoke(() => Queued?.Invoke(mod));
            AppLogger.Info($"[BROWSE_BRIDGE] Queued '{mod.Name}' ({mod.TypeLabel}) from {displayName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[BROWSE_BRIDGE] Detection failed for {displayName}", ex);
            UiDispatcher.Invoke(() => Failed?.Invoke(displayName, ex.Message));
        }
    }

    private static string CleanDisplayName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        stem = System.Text.RegularExpressions.Regex.Replace(stem, @"[_\-\.]+", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(stem.Trim().ToLowerInvariant());
    }
}
