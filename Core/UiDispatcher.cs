using System.Windows;
using System.Windows.Threading;

namespace LSPDFRManager.Core;

/// <summary>
/// Centralizes safe access to the WPF UI dispatcher so services and view models
/// can update UI-bound state without repeating boilerplate.
/// </summary>
public static class UiDispatcher
{
    private static Dispatcher? Current => Application.Current?.Dispatcher;

    public static bool CheckAccess()
    {
        var dispatcher = Current;
        return dispatcher is null ||
            dispatcher.HasShutdownStarted ||
            dispatcher.HasShutdownFinished ||
            dispatcher.CheckAccess();
    }

    public static void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Current;
        if (dispatcher is null ||
            dispatcher.HasShutdownStarted ||
            dispatcher.HasShutdownFinished ||
            dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    public static void BeginInvoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Current;
        if (dispatcher is null ||
            dispatcher.HasShutdownStarted ||
            dispatcher.HasShutdownFinished ||
            dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }
}
