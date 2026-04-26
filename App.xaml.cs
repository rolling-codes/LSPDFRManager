using System.Windows;
using LSPDFRManager.Core;

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
            AppLogger.Info("[APP_STARTUP] Calling base.OnStartup");
            base.OnStartup(e);

            AppLogger.Info("[APP_STARTUP] Creating MainWindow");
            var window = new MainWindow();

            AppLogger.Info("[APP_STARTUP] Showing MainWindow");
            window.Show();

            AppLogger.Info("[APP_STARTUP] Success");
        }
        catch (Exception ex)
        {
            AppLogger.Error("[APP_STARTUP] Failed", ex);
            throw;
        }
    }
}
