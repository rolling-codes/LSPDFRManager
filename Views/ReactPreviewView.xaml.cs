using System.IO;
using System.Windows;
using System.Windows.Controls;
using LSPDFRManager.Core;
using LSPDFRManager.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace LSPDFRManager.Views;

public partial class ReactPreviewView : UserControl
{
    private ReactPreviewViewModel? Vm => DataContext as ReactPreviewViewModel;

    public ReactPreviewView() => InitializeComponent();

    private async void ReactPreviewView_Loaded(object sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        // Wait for LocalApiHost to be ready, then initialize WebView2
        try
        {
            await vm.WaitForReadyAsync();

            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LSPDFRManager", "WebView2React");

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: profileDir);
            await WebView.EnsureCoreWebView2Async(env);

            WebView.Source = new Uri(vm.Uri);

            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[REACT_PREVIEW] WebView2 initialization failed", ex);
            StatusLabel.Text = $"Failed to load React UI: {ex.Message}";
        }
    }
}
