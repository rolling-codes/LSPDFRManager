using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class PatrolReadinessViewModel : ObservableObject
{
    private readonly PatrolReadinessService _service = new();
    private PatrolReadinessResult? _result;
    private bool _isChecking;

    public PatrolReadinessViewModel()
    {
        CheckCommand = new RelayCommand(async () => await CheckAsync());
    }

    public ICommand CheckCommand { get; }

    public PatrolReadinessResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(OverallState));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(BlockingIssues));
                OnPropertyChanged(nameof(Warnings));
                OnPropertyChanged(nameof(PassingChecks));
                OnPropertyChanged(nameof(HasBlockingIssues));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(HasPassingChecks));
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(CheckedAt));
            }
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    public PatrolReadinessState OverallState => _result?.OverallState ?? PatrolReadinessState.Unknown;

    public string StatusText => OverallState switch
    {
        PatrolReadinessState.Ready    => "READY TO PATROL",
        PatrolReadinessState.Warning  => "WARNINGS",
        PatrolReadinessState.NotReady => "NOT READY",
        _                             => "UNKNOWN",
    };

    public IReadOnlyList<string> BlockingIssues => _result?.BlockingIssues ?? [];
    public IReadOnlyList<string> Warnings       => _result?.Warnings ?? [];
    public IReadOnlyList<string> PassingChecks  => _result?.PassingChecks ?? [];

    public bool HasBlockingIssues => BlockingIssues.Count > 0;
    public bool HasWarnings       => Warnings.Count > 0;
    public bool HasPassingChecks  => PassingChecks.Count > 0;
    public bool HasResult         => _result is not null;

    public string CheckedAt => _result is null
        ? ""
        : $"Checked {_result.CheckedAtUtc.ToLocalTime():HH:mm:ss}";

    public async Task CheckAsync()
    {
        IsChecking = true;
        try
        {
            Result = await _service.CheckAsync().ConfigureAwait(false);
        }
        finally
        {
            IsChecking = false;
        }
    }
}
