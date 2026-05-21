using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.PatrolReadiness;
using LSPDFRManager.Services;

namespace LSPDFRManager.ViewModels;

public class PatrolReadinessDashboardViewModel : ObservableObject
{
    private readonly IPatrolReadinessController _controller;
    private PatrolReadinessSummary _summary = PatrolReadinessSummary.Empty;
    private bool _isBusy;
    private string _statusMessage = "Press Scan to check your setup.";
    private bool _hasScanned;

    public PatrolReadinessDashboardViewModel()
        : this(new PatrolReadinessController()) { }

    internal PatrolReadinessDashboardViewModel(IPatrolReadinessController controller)
    {
        _controller = controller;
        ScanCommand          = new RelayCommand(async () => await ScanAsync(), () => IsIdle);
        MarkKnownGoodCommand = new RelayCommand(OnMarkKnownGood, () => IsIdle && HasScanned && IsReady);
        ExportBundleCommand  = new RelayCommand(async () => await ExportBundleAsync(), () => IsIdle);
    }

    // ── Scan result binding ───────────────────────────────────────────────────

    public PatrolReadinessSummary Summary
    {
        get => _summary;
        private set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusEmoji));
                OnPropertyChanged(nameof(Score));
                OnPropertyChanged(nameof(ScoreText));
                OnPropertyChanged(nameof(BlockingIssues));
                OnPropertyChanged(nameof(Warnings));
                OnPropertyChanged(nameof(Info));
                OnPropertyChanged(nameof(HasBlockingIssues));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(HasInfo));
                OnPropertyChanged(nameof(HasKnownGoodDiff));
                OnPropertyChanged(nameof(KnownGoodDiffText));
                OnPropertyChanged(nameof(ScannedAtText));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsNotReady));
                OnPropertyChanged(nameof(IsWarning));
            }
        }
    }

    public PatrolReadinessState Status => _summary.Status;
    public int Score => _summary.Score;
    public string ScoreText => $"{_summary.Score}/100";

    public string StatusLabel => _summary.Status switch
    {
        PatrolReadinessState.Ready    => "Ready for Patrol",
        PatrolReadinessState.Warning  => "Needs Attention",
        PatrolReadinessState.NotReady => "Not Ready",
        _                             => "Not Scanned",
    };

    public string StatusEmoji => _summary.Status switch
    {
        PatrolReadinessState.Ready    => "✅",
        PatrolReadinessState.Warning  => "⚠",
        PatrolReadinessState.NotReady => "🚫",
        _                             => "—",
    };

    public bool IsReady    => _summary.Status == PatrolReadinessState.Ready;
    public bool IsWarning  => _summary.Status == PatrolReadinessState.Warning;
    public bool IsNotReady => _summary.Status == PatrolReadinessState.NotReady;

    public IReadOnlyList<InstallIssue> BlockingIssues => _summary.BlockingIssues;
    public IReadOnlyList<InstallIssue> Warnings       => _summary.Warnings;
    public IReadOnlyList<InstallIssue> Info           => _summary.Info;

    public bool HasBlockingIssues => _summary.BlockingIssues.Count > 0;
    public bool HasWarnings       => _summary.Warnings.Count > 0;
    public bool HasInfo           => _summary.Info.Count > 0;

    public bool HasKnownGoodDiff => _summary.KnownGoodDiffSummary is { HasChanges: true };
    public string KnownGoodDiffText
    {
        get
        {
            var diff = _summary.KnownGoodDiffSummary;
            if (diff is null) return "No known-good baseline recorded.";
            if (!diff.HasChanges) return "No changes since last known-good.";
            var parts = new List<string>();
            if (diff.AddedPlugins.Count > 0)   parts.Add($"{diff.AddedPlugins.Count} added");
            if (diff.RemovedPlugins.Count > 0)  parts.Add($"{diff.RemovedPlugins.Count} removed");
            if (diff.ChangedConfigs.Count > 0)  parts.Add($"{diff.ChangedConfigs.Count} configs changed");
            return string.Join(", ", parts) + " since last known-good.";
        }
    }

    public string ScannedAtText => _summary.ScannedAt == default
        ? string.Empty
        : $"Last scanned: {_summary.ScannedAt.ToLocalTime():yyyy-MM-dd HH:mm}";

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }
    public bool IsIdle => !_isBusy;

    public bool HasScanned
    {
        get => _hasScanned;
        private set => SetProperty(ref _hasScanned, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ScanCommand { get; }
    public ICommand MarkKnownGoodCommand { get; }
    public ICommand ExportBundleCommand { get; }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning…";
        try
        {
            var result = await Task.Run(() => _controller.ScanAsync()).ConfigureAwait(false);
            Core.UiDispatcher.Invoke(() =>
            {
                Summary = result;
                HasScanned = true;
                StatusMessage = result.Status switch
                {
                    PatrolReadinessState.Ready    => "All checks passed.",
                    PatrolReadinessState.Warning  => $"{result.Warnings.Count} warning(s) detected.",
                    PatrolReadinessState.NotReady => $"{result.BlockingIssues.Count} blocking issue(s) found.",
                    _                             => "Scan complete.",
                };
            });
        }
        catch (Exception ex)
        {
            Core.AppLogger.Error($"[PatrolReadiness] Scan failed: {ex.Message}");
            Core.UiDispatcher.Invoke(() => StatusMessage = $"Scan failed: {ex.Message}");
        }
        finally
        {
            Core.UiDispatcher.Invoke(() =>
            {
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            });
        }
    }

    private void OnMarkKnownGood()
    {
        try
        {
            _controller.MarkKnownGood();
            StatusMessage = "Current state saved as known-good.";
            OnPropertyChanged(nameof(KnownGoodDiffText));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not mark known-good: {ex.Message}";
        }
    }

    private async Task ExportBundleAsync()
    {
        IsBusy = true;
        StatusMessage = "Exporting support bundle…";
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title    = "Save Support Bundle",
                Filter   = "ZIP Archive|*.zip",
                FileName = $"LSPDFRManager-support-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            };
            if (dlg.ShowDialog() != true)
            {
                StatusMessage = "Export cancelled.";
                return;
            }
            var path = await new SupportBundleService().ExportAsync(dlg.FileName).ConfigureAwait(false);
            Core.UiDispatcher.Invoke(() => StatusMessage = $"Bundle saved: {path}");
        }
        catch (Exception ex)
        {
            Core.AppLogger.Error($"[PatrolReadiness] Bundle export failed: {ex.Message}");
            Core.UiDispatcher.Invoke(() => StatusMessage = $"Export failed: {ex.Message}");
        }
        finally
        {
            Core.UiDispatcher.Invoke(() => IsBusy = false);
        }
    }
}
