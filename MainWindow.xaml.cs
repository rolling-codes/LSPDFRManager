using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.Views;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class MainWindow : Window
{
    private LoadingView? _loadingView;
    private MainViewModel? _mainViewModel;

    public MainWindow()
    {
        AppLogger.Info("[MAINWINDOW] Constructor called");
        try
        {
            AppLogger.Info("[MAINWINDOW] InitializeComponent starting");
            InitializeComponent();
            AppLogger.Info("[MAINWINDOW] InitializeComponent completed");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW] InitializeComponent failed", ex);
            throw;
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        AppLogger.Info("[MAINWINDOW] OnInitialized starting");
        try
        {
            base.OnInitialized(e);

            AppLogger.Info("[MAINWINDOW] Showing loading view");
            _loadingView = new LoadingView();
            _loadingView.SetStatus("Initializing services...");
            Content = _loadingView;

            AppLogger.Info("[MAINWINDOW] Creating MainViewModel");
            _mainViewModel = new MainViewModel();

            AppLogger.Info("[MAINWINDOW] Switching to MainViewModel");
            DataContext = _mainViewModel;
            Content = _mainViewModel;

            AppLogger.Info("[MAINWINDOW] OnInitialized completed successfully");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[MAINWINDOW] OnInitialized failed", ex);
            if (_loadingView != null)
            {
                _loadingView.SetError($"Failed to load: {ex.Message}");
                Content = _loadingView;
            }
            throw;
        }
    }
}
