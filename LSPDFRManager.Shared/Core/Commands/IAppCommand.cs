namespace LSPDFRManager.Core.Commands;

public interface IAppCommand
{
    event EventHandler? CanExecuteChanged;

    bool CanExecute(object? parameter = null);

    Task ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default);
}
