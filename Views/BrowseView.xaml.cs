using System.IO;
using System.Windows;
using System.Windows.Controls;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace LSPDFRManager.Views;

public partial class BrowseView : UserControl
{
    private BrowseViewModel? Vm => DataContext as BrowseViewModel;

    public BrowseView() => InitializeComponent();

    private async void BrowseView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Persistent profile so cookies/login survive between app launches
            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LSPDFRManager", "WebView2");

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: profileDir);

            await WebView.EnsureCoreWebView2Async(env);
            WebView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
        }
        catch (Exception ex)
        {
            if (Vm is not null)
                Vm.StatusMessage = $"WebView2 init failed: {ex.Message}";
        }
    }

    private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        // Suppress the save dialog — let WebView2 download to a temp path we control
        var tempDir = Path.Combine(Path.GetTempPath(), "LSPDFRManager_downloads");
        Directory.CreateDirectory(tempDir);

        var suggestedName = string.IsNullOrWhiteSpace(e.DownloadOperation.ResultFilePath)
            ? $"mod_{DateTime.Now:yyyyMMddHHmmss}.zip"
            : Path.GetFileName(e.DownloadOperation.ResultFilePath);

        var destPath = Path.Combine(tempDir, SanitizeFileName(suggestedName));
        e.ResultFilePath = destPath;
        e.Handled = true; // suppress Save dialog

        var op = e.DownloadOperation;

        op.StateChanged += (s, _) =>
        {
            if (op.State == CoreWebView2DownloadState.Completed)
            {
                Dispatcher.Invoke(() =>
                {
                    ModDownloadBridge.Instance.OnDownloadCompleted(destPath, suggestedName);
                });
            }
            else if (op.State == CoreWebView2DownloadState.Interrupted)
            {
                Dispatcher.Invoke(() =>
                {
                    if (Vm is not null)
                        Vm.StatusMessage = $"Download interrupted: {op.InterruptReason}";
                });
            }
        };

        // Show progress in the status bar
        op.BytesReceivedChanged += (s, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (Vm is null) return;
                var total = (long)(op.TotalBytesToReceive ?? 0);
                var received = op.BytesReceived;
                Vm.StatusMessage = total > 0
                    ? $"Downloading {suggestedName}… {received / 1024:N0} KB / {total / 1024:N0} KB"
                    : $"Downloading {suggestedName}… {received / 1024:N0} KB";
                if (total > 0)
                    Vm.LoadProgress = (int)(received * 100 / total);
            });
        };

        Dispatcher.Invoke(() =>
        {
            if (Vm is not null)
            {
                Vm.IsLoading = true;
                Vm.StatusMessage = $"Starting download: {suggestedName}";
            }
        });
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Vm is null) return;
        Vm.IsLoading = true;
        Vm.CurrentUrl = e.Uri;
        Vm.StatusMessage = $"Loading…";
        UpdateNavButtons();
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Vm is null) return;
        Vm.IsLoading = false;
        Vm.LoadProgress = 100;
        Vm.CurrentUrl = WebView.Source?.ToString() ?? "";
        Vm.StatusMessage = e.IsSuccess ? "Ready" : $"Failed to load page ({e.WebErrorStatus})";
        UpdateNavButtons();
    }

    private void UpdateNavButtons()
    {
        BackButton.IsEnabled    = WebView.CanGoBack;
        ForwardButton.IsEnabled = WebView.CanGoForward;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)    { if (WebView.CanGoBack)    WebView.GoBack(); }
    private void ForwardButton_Click(object sender, RoutedEventArgs e) { if (WebView.CanGoForward) WebView.GoForward(); }
    private void RefreshButton_Click(object sender, RoutedEventArgs e) { WebView.Reload(); }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        // Inject JS to click the primary download button on the lcpdfr.com mod page
        // This triggers the site's download flow, which fires DownloadStarting above
        try
        {
            await WebView.CoreWebView2.ExecuteScriptAsync("""
                (function() {
                    // lcpdfr.com download button selectors (try most specific first)
                    var btn = document.querySelector('a[data-action="download"]')
                           || document.querySelector('a.ipsButton_primary[href*="do=download"]')
                           || document.querySelector('a[href*="do=download"]')
                           || document.querySelector('a.ipsButton[href*="download"]');
                    if (btn) { btn.click(); return 'clicked'; }
                    return 'not_found';
                })()
                """);
        }
        catch (Exception ex)
        {
            if (Vm is not null)
                Vm.StatusMessage = $"Could not trigger download: {ex.Message}";
        }
    }

    public void NavigateTo(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        WebView.Source = new Uri(url);
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars()));
}
