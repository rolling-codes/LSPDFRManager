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
        var wizardWillRun = AppConfig.Instance.ShowSetupWizardOnStartup
            || string.IsNullOrWhiteSpace(gtaPath);

        if (!wizardWillRun)
        {
            if (!Directory.Exists(gtaPath))
            {
                issues.Add($"GTA V installation folder not found:\n  {gtaPath}\n  Open Settings to set the correct path.");
            }
            else
            {
                if (LspdfrInstallLocator.FindGtaExe(gtaPath) is null)
                    issues.Add($"GTA V executable not found in:\n  {gtaPath}\n  Verify Settings points at the GTA V installation folder.");

                var writeProbe = Path.Combine(gtaPath, ".lspdfrmanager_write_test");
                try
                {
                    File.WriteAllText(writeProbe, "");
                    File.Delete(writeProbe);
                }
                catch
                {
                    issues.Add($"GTA V folder is not writable:\n  {gtaPath}\n  The app must run as Administrator to install mods into a protected directory.");
                }
            }
        }

        AddDiskSpaceIssueIfNeeded(issues, AppDataPaths.Root, "App data");
        if (!string.IsNullOrWhiteSpace(gtaPath) && Directory.Exists(gtaPath))
            AddDiskSpaceIssueIfNeeded(issues, gtaPath, "GTA V install");

        if (issues.Count == 0)
            return;

        var message = string.Join("\n\n", issues) +
            "\n\nThe app will open but some features may not work correctly.";

        MessageBox.Show(message, "LSPDFR Manager — Startup Issues",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void AddDiskSpaceIssueIfNeeded(List<string> issues, string path, string label)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
                return;

            var drive = new DriveInfo(root);
            var requiredBytes = (long)AppConfig.Instance.MinimumFreeDiskSpaceMb * 1024 * 1024;
            if (drive.AvailableFreeSpace < requiredBytes)
            {
                issues.Add(
                    $"{label} drive is low on free space:\n  {root}\n  Available: {drive.AvailableFreeSpace / 1024 / 1024:N0} MB; required: {AppConfig.Instance.MinimumFreeDiskSpaceMb:N0} MB.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Disk-space check failed for '{path}': {ex.Message}");
        }
    }
}
