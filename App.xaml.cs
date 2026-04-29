using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Info("[APP_START] Application startup initiated");

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            AppLogger.Error("[UNHANDLED_EXCEPTION]", (Exception)ex.ExceptionObject);
        };

        DispatcherUnhandledException += (s, ex) =>
        {
            AppLogger.Error("[UI_EXCEPTION]", ex.Exception);
            ex.Handled = false;
        };

        try
        {
            base.OnStartup(e);

            var vm = new MainViewModel();
            var window = new MainWindow(vm);
            window.Show();
        }
        catch (Exception ex)
        {
            AppLogger.Error("[APP_STARTUP] Failed", ex);
            throw;
        }
    }
}
