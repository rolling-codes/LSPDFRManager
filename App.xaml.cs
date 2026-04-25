using System.Windows;

namespace LSPDFRManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED EXCEPTION\n{ex.ExceptionObject}\n";
            try { File.AppendAllText("crash.log", msg); } catch { }
        };

        DispatcherUnhandledException += (s, ex) =>
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UI EXCEPTION\n{ex.Exception}\n";
            try { File.AppendAllText("crash.log", msg); } catch { }
            ex.Handled = false;
        };

        try
        {
            base.OnStartup(e);
            var window = new MainWindow();
            window.Show();
        }
        catch (Exception ex)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STARTUP FAILED\n{ex}\n";
            try { File.AppendAllText("crash.log", msg); } catch { }
            throw;
        }
    }
}
