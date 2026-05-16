using LSPDFRManager.Core;

namespace LSPDFRManager.Core.Commands;

public sealed class AsyncAppCommand : IAppCommand
{
    private readonly Func<object?, CancellationToken, Task> _executeAsync;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncAppCommand(
        Func<object?, CancellationToken, Task> executeAsync,
        Func<object?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
                return;

            _isRunning = value;
            UiDispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    public bool CanExecute(object? parameter = null) =>
        !IsRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async Task ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(parameter))
            return;

        IsRunning = true;
        try
        {
            await _executeAsync(parameter, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsRunning = false;
        }
    }
}
