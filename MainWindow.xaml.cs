using System.Windows;
using LSPDFRManager.Core;

namespace LSPDFRManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AppLogger.Info("[MAINWINDOW_INIT] Constructor called");
        try
        {
            AppLogger.Info("[MAINWINDOW_INIT] InitializeComponent starting");
            InitializeComponent();
            AppLogger.Info("[MAINWINDOW_INIT] InitializeComponent completed");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW_INIT] InitializeComponent failed", ex);
            throw;
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        AppLogger.Info("[MAINWINDOW_INIT] OnInitialized starting");
        try
        {
            base.OnInitialized(e);
            AppLogger.Info("[MAINWINDOW_INIT] OnInitialized completed");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW_INIT] OnInitialized failed", ex);
            throw;
        }
    }
}
