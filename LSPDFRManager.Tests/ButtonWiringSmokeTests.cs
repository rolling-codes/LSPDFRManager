using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LSPDFRManager.ViewModels;
using LSPDFRManager.Views;
using Xunit;

namespace LSPDFRManager.Tests;

public class ButtonWiringSmokeTests : CommandCenterTestBase
{
    [Fact]
    public void MainNavigationAndChangedButtons_RenderAndExecuteWithoutCrashing()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunSmoke();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF button smoke test timed out.");

        if (failure is not null)
            throw failure;
    }

    private void RunSmoke()
    {
        EnsureApplicationResources();
        PrepareFakeGta();

        var main = new MainViewModel();

        AssertNavigation(main, "Home", typeof(DashboardViewModel));
        AssertNavigation(main, "Library", typeof(LibraryViewModel));
        AssertNavigation(main, "Install", typeof(InstallViewModel));
        AssertNavigation(main, "Browse", typeof(BrowseViewModel));
        AssertNavigation(main, "PatrolReadiness", typeof(PatrolReadinessDashboardViewModel));
        AssertNavigation(main, "Diagnostics", typeof(DiagnosticsViewModel));
        AssertNavigation(main, "Profiles", typeof(ProfilesViewModel));
        AssertNavigation(main, "Backups", typeof(BackupsViewModel));
        AssertNavigation(main, "History", typeof(HistoryViewModel));
        AssertNavigation(main, "Logs", typeof(LogViewerViewModel));
        AssertNavigation(main, "Settings", typeof(SettingsViewModel));
        AssertNavigation(main, "ModConfig", typeof(ConfigViewModel));
        AssertNavigation(main, "Oiv", typeof(OivViewModel));
        AssertNavigation(main, "DevDiagnostics", typeof(DevDiagnosticsViewModel));

        var dashboard = main.DashboardVM;
        dashboard.ScanPluginsCommand.Execute(null);
        dashboard.AnalyzeCrashLogsCommand.Execute(null);
        dashboard.CreateBackupCommand.Execute(null);
        dashboard.ApplySafeLaunchCommand.Execute(null);
        dashboard.LaunchGtaCommand.Execute(null);
        dashboard.LaunchRphCommand.Execute(null);

        var patrolView = Render(new PatrolReadinessDashboardView { DataContext = main.PatrolReadinessVM });
        ClickRequiredButton(patrolView, "Scan Now");

        var diagnosticsView = Render(new DevDiagnosticsView { DataContext = main.DevDiagnosticsVM });
        ClickRequiredButton(diagnosticsView, "Refresh Log");
    }

    private static void AssertNavigation(MainViewModel main, string page, Type expectedViewModel)
    {
        main.NavigateCommand.Execute(page);
        Assert.IsType(expectedViewModel, main.CurrentView);
    }

    private void PrepareFakeGta()
    {
        File.WriteAllText(Path.Combine(GtaDir, "GTA5.exe"), "not a real exe");
        File.WriteAllText(Path.Combine(GtaDir, "RAGEPluginHook.exe"), "not a real exe");
        File.WriteAllText(Path.Combine(GtaDir, "ScriptHookV.dll"), "fake shv");
        Directory.CreateDirectory(Path.Combine(GtaDir, "plugins", "LSPDFR"));
        Directory.CreateDirectory(Path.Combine(GtaDir, "scripts"));
        File.WriteAllText(Path.Combine(GtaDir, "plugins", "LSPDFR", "LSPDFR.dll"), "fake lspdfr");
        File.WriteAllText(Path.Combine(GtaDir, "scripts", "ButtonSmoke.cs"), "// fake script");
    }

    private static Window Render(object content)
    {
        var window = new Window
        {
            Content = content,
            Width = 1320,
            Height = 840,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void ClickRequiredButton(DependencyObject root, string content)
    {
        var button = FindVisualChildren<Button>(root)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal));

        Assert.NotNull(button);
        Assert.True(button!.IsEnabled, $"Button '{content}' should be enabled.");
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static void EnsureApplicationResources()
    {
        var app = Application.Current ?? new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        if (app.Resources.MergedDictionaries.Count > 0)
            return;

        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/LSPDFRManager;component/Resources/Styles.xaml"),
        });
    }
}
