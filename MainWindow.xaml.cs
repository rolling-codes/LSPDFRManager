using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        AppLogger.Info("[MAINWINDOW] Constructor called");
        try
        {
            AppLogger.Info("[MAINWINDOW] InitializeComponent starting");
            InitializeComponent();
            AppLogger.Info("[MAINWINDOW] InitializeComponent completed");

            DataContext = viewModel;
            AppLogger.Info("[MAINWINDOW] DataContext assigned");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW] Initialization failed", ex);
            throw;
        }
    }
}
