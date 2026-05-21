using System.Windows.Input;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.SafeMode;

namespace LSPDFRManager.ViewModels;

public enum SafeModePhase { Idle, Confirming, Applying, Done }

public sealed record SafeModeOption(string Mode, string Label, string Description);

public class SafeModeViewModel : ObservableObject
{
    private readonly ISafeModeController _controller;
    private SafeModePhase _phase = SafeModePhase.Idle;
    private SafeModeOption _selectedMode;
    private SafeLaunchPlan? _plan;
    private SafeModeApplyResult? _result;
    private bool _isBusy;
    private string _statusMessage = "Select a mode and preview the changes before applying.";

    public static readonly IReadOnlyList<SafeModeOption> Modes =
    [
        new("LspdfrOnly",             "LSPDFR Only",               "Disable all plugins except LSPDFR itself. Isolates LSPDFR-level issues."),
        new("VanillaGtaV",            "Vanilla GTA V",             "Disable all plugins including LSPDFR. Use for base-game diagnostics."),
        new("DisableRecentMods",      "Disable Recent Mods",       "Disable plugins installed or modified in the last 7 days."),
        new("DisableNonEssentialAsi", "Disable Non-Essential ASI", "Disable .asi mods except ScriptHookV, OpenIV, HeapAdjuster, PackfileLimitAdjuster."),
        new("DisableScripts",         "Disable Scripts",           "Disable .cs/.vb/.lua scripts in the scripts/ folder."),
    ];

    public SafeModeViewModel()
        : this(new SafeModeController()) { }

    internal SafeModeViewModel(ISafeModeController controller)
    {
        _controller   = controller;
        _selectedMode = Modes[0];

        PreviewCommand      = new RelayCommand(async () => await PreviewAsync(), () => IsIdle && !IsBusy);
        ConfirmApplyCommand = new RelayCommand(async () => await ApplyAsync(),   () => IsConfirming && !IsBusy);
        ResetCommand        = new RelayCommand(Reset, () => !IsApplying);
    }

    // ── Phase ─────────────────────────────────────────────────────────────────

    public SafeModePhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsConfirming));
                OnPropertyChanged(nameof(IsApplying));
                OnPropertyChanged(nameof(IsDone));
            }
        }
    }

    public bool IsIdle       => _phase == SafeModePhase.Idle;
    public bool IsConfirming => _phase == SafeModePhase.Confirming;
    public bool IsApplying   => _phase == SafeModePhase.Applying;
    public bool IsDone       => _phase == SafeModePhase.Done;

    // ── Mode selection ────────────────────────────────────────────────────────

    public SafeModeOption SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    // ── Plan / preview ────────────────────────────────────────────────────────

    public SafeLaunchPlan? Plan
    {
        get => _plan;
        private set
        {
            if (SetProperty(ref _plan, value))
            {
                OnPropertyChanged(nameof(PlanChanges));
                OnPropertyChanged(nameof(PlanSummaryText));
                OnPropertyChanged(nameof(HasPlanChanges));
            }
        }
    }

    public IReadOnlyList<string> PlanChanges =>
        _plan?.Changes.Select(c => Path.GetFileName(c.FilePath)).ToList() ?? [];

    public string PlanSummaryText => _plan is null
        ? ""
        : $"{_plan.Changes.Count} file(s) will be disabled.";

    public bool HasPlanChanges => _plan?.Changes.Count > 0;

    // ── Busy / status ─────────────────────────────────────────────────────────

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<string> ProgressLog { get; } = [];

    // ── Result ────────────────────────────────────────────────────────────────

    public SafeModeApplyResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(ResultIsSuccess));
                OnPropertyChanged(nameof(ResultMessage));
                OnPropertyChanged(nameof(RestorePointHint));
            }
        }
    }

    public bool   ResultIsSuccess  => _result?.Success == true;
    public string ResultMessage    => _result?.StatusMessage ?? "";
    public string RestorePointHint => _result is null ? "" :
        $"To undo: open Backups → Restore Points and restore ID '{_result.RestorePointId[..8]}'.";

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand PreviewCommand      { get; }
    public ICommand ConfirmApplyCommand { get; }
    public ICommand ResetCommand        { get; }

    private async Task PreviewAsync()
    {
        IsBusy = true;
        StatusMessage = "Building preview…";
        try
        {
            var plan = await Task.Run(() => _controller.BuildPreviewAsync(_selectedMode.Mode))
                                 .ConfigureAwait(false);
            UiDispatcher.Invoke(() =>
            {
                Plan = plan;
                Phase = SafeModePhase.Confirming;
                StatusMessage = plan.Changes.Count == 0
                    ? "No files matched this mode — nothing to disable."
                    : $"Preview ready: {plan.Changes.Count} file(s) will be disabled.";
                CommandManager.InvalidateRequerySuggested();
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[SafeMode] Preview failed: {ex.Message}", ex);
            UiDispatcher.Invoke(() => StatusMessage = $"Preview failed: {ex.Message}");
        }
        finally
        {
            UiDispatcher.Invoke(() =>
            {
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            });
        }
    }

    private async Task ApplyAsync()
    {
        if (_plan is null) return;
        Phase = SafeModePhase.Applying;
        ProgressLog.Clear();
        StatusMessage = "Applying Safe Mode…";

        var progress = new Progress<string>(msg =>
            UiDispatcher.Invoke(() => ProgressLog.Add(msg)));

        try
        {
            var result = await Task.Run(() => _controller.ApplyAsync(_plan, progress))
                                   .ConfigureAwait(false);
            UiDispatcher.Invoke(() =>
            {
                Result = result;
                Phase  = SafeModePhase.Done;
                StatusMessage = result.Success
                    ? "Safe Mode applied successfully."
                    : $"Applied with {result.FilesFailed} error(s). See progress log.";
                CommandManager.InvalidateRequerySuggested();
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[SafeMode] Apply failed: {ex.Message}", ex);
            UiDispatcher.Invoke(() =>
            {
                StatusMessage = $"Apply failed: {ex.Message}";
                Phase = SafeModePhase.Confirming;
                CommandManager.InvalidateRequerySuggested();
            });
        }
    }

    private void Reset()
    {
        Plan = null;
        Result = null;
        ProgressLog.Clear();
        Phase = SafeModePhase.Idle;
        StatusMessage = "Select a mode and preview the changes before applying.";
        CommandManager.InvalidateRequerySuggested();
    }
}
