using System.Windows;
using System.Windows.Media;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
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
            ApplyScale(AppConfig.Instance.UiScale);
            AppConfig.UiScaleChanged += ApplyScale;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW] Initialization failed", ex);
            throw;
        }
    }

    private void ApplyScale(double scale)
    {
        AppScaleTransform.ScaleX = scale;
        AppScaleTransform.ScaleY = scale;
    }

    private void DismissError_Click(object _, RoutedEventArgs __)
    {
        if (DataContext is MainViewModel vm)
            vm.GlobalErrorMessage = null;
    }
}
