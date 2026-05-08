using System.Windows;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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

            ValidateStartup();

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

    private static void ValidateStartup()
    {
        var issues = new List<string>();

        try
        {
            AppDataPaths.EnsureRootExists();

            var probe = Path.Combine(AppDataPaths.Root, $".write_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            issues.Add($"App data folder is not writable:\n  {AppDataPaths.Root}\n  ({ex.Message})");
        }

        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
        {
            issues.Add($"GTA V installation folder not found:\n  {gtaPath}\n  Open Settings to set the correct path.");
        }

        if (issues.Count == 0)
            return;

        var message = string.Join("\n\n", issues) +
            "\n\nThe app will open but some features may not work correctly.";

        MessageBox.Show(message, "LSPDFR Manager — Startup Issues",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
