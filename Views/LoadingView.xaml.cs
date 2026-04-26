using System.Windows.Controls;
using LSPDFRManager.Core;

namespace LSPDFRManager.Views;

public partial class LoadingView : UserControl
{
    public LoadingView()
    {
        AppLogger.Info("[LOADINGVIEW] Creating LoadingView");
        InitializeComponent();
        AppLogger.Info("[LOADINGVIEW] LoadingView initialized");
    }

    public void SetStatus(string message)
    {
        AppLogger.Info($"[LOADINGVIEW] Status: {message}");
        StatusText.Text = message;
    }

    public void SetError(string error)
    {
        AppLogger.Error($"[LOADINGVIEW] Error: {error}", null);
        ErrorText.Text = error;
    }
}
