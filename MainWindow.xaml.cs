using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        try
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW] Initialization failed", ex);
            throw;
        }
    }
}
