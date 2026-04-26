using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.Views;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class MainWindow : Window
{
    private LoadingView? _loadingView;
    private MainViewModel? _mainViewModel;
    private UIElement? _originalContent;

    public MainWindow()
    {
        AppLogger.Info("[MAINWINDOW] Constructor called");
        try
        {
            AppLogger.Info("[MAINWINDOW] InitializeComponent starting");
            InitializeComponent();
            _originalContent = Content as UIElement;
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

            // Simulate some background work if needed, or just proceed
            AppLogger.Info("[MAINWINDOW] Creating MainViewModel");
            _mainViewModel = new MainViewModel();

            AppLogger.Info("[MAINWINDOW] Switching to MainViewModel");
            DataContext = _mainViewModel;

            // Restore original XAML content (the Grid with sidebar and content area)
            Content = _originalContent;

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
