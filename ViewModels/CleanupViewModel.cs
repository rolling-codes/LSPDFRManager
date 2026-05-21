using System.Windows.Input;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.Services.Modes;

namespace LSPDFRManager.ViewModels;

public enum CleanupStep { ModeSelect, Preview, Confirm, Result }

public sealed class SelectableCandidate : ObservableObject
{
    private bool _isSelected;

    public RemovalCandidate Candidate { get; }
    public string Label => Candidate.RelativePath;
    public string RiskLabel => Candidate.Reason;
    public bool IsBlocked => Candidate.Classification == CandidateClassification.Blocked;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SelectableCandidate(RemovalCandidate candidate, bool selected)
    {
        Candidate = candidate;
        _isSelected = selected;
    }
}

public sealed class SelectableGroup
{
    public string Label { get; }
    public IReadOnlyList<SelectableCandidate> Items { get; }

    public SelectableGroup(RemovalGroup group, HashSet<Guid> defaultSelectedIds)
    {
        Label = group.Label;
        Items = group.Candidates
            .Select(c => new SelectableCandidate(c, defaultSelectedIds.Contains(c.Id)))
            .ToList();
    }
}

public class CleanupViewModel : ObservableObject
{
    private readonly CleanupApplyService _applyService;

    private CleanupStep _step = CleanupStep.ModeSelect;
    private CleanupMode _selectedMode = CleanupMode.SafeCoreReset;
    private CleanupModePreset? _preset;
    private CleanupApplyResult? _applyResult;
    private bool _isBusy;
    private string _statusMessage = "";

    public CleanupViewModel()
        : this(new CleanupApplyService()) { }

    internal CleanupViewModel(CleanupApplyService applyService)
    {
        _applyService = applyService;

        NextCommand    = new RelayCommand(async () => await NextAsync(), CanNext);
        BackCommand    = new RelayCommand(Back,  () => Step == CleanupStep.Preview || Step == CleanupStep.Confirm);
        CancelCommand  = new RelayCommand(Cancel);
        ConfirmCommand = new RelayCommand(async () => await ApplyAsync(), CanConfirm);
    }

    public CleanupStep Step
    {
        get => _step;
        private set
        {
            if (!SetProperty(ref _step, value)) return;
            OnPropertyChanged(nameof(IsModeSelectStep));
            OnPropertyChanged(nameof(IsPreviewStep));
            OnPropertyChanged(nameof(IsConfirmStep));
            OnPropertyChanged(nameof(IsResultStep));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public CleanupMode SelectedMode
    {
        get => _selectedMode;
        set { if (SetProperty(ref _selectedMode, value)) OnPropertyChanged(nameof(SelectedModeIndex)); }
    }

    // Int-valued alias for IntEqualityConverter RadioButton binding
    public int SelectedModeIndex
    {
        get => (int)_selectedMode;
        set => SelectedMode = (CleanupMode)value;
    }

    public CleanupModePreset? Preset
    {
        get => _preset;
        private set
        {
            SetProperty(ref _preset, value);
            OnPropertyChanged(nameof(WarningText));
            OnPropertyChanged(nameof(RiskLabel));
        }
    }

    public CleanupApplyResult? ApplyResult
    {
        get => _applyResult;
        private set => SetProperty(ref _applyResult, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { SetProperty(ref _isBusy, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<SelectableGroup> Groups { get; } = [];

    public bool IsModeSelectStep => Step == CleanupStep.ModeSelect;
    public bool IsPreviewStep    => Step == CleanupStep.Preview;
    public bool IsConfirmStep    => Step == CleanupStep.Confirm;
    public bool IsResultStep     => Step == CleanupStep.Result;

    public string WarningText => Preset?.WarningText ?? "";
    public string RiskLabel   => Preset?.RiskLevel.ToString() ?? "";

    public ICommand NextCommand    { get; }
    public ICommand BackCommand    { get; }
    public ICommand CancelCommand  { get; }
    public ICommand ConfirmCommand { get; }

    public Action? OnCancelled { get; set; }

    // ── Commands ───────────────────────────────────────────────────────────────

    private bool CanNext() => Step == CleanupStep.ModeSelect && !IsBusy;

    private async Task NextAsync()
    {
        if (Step != CleanupStep.ModeSelect) return;

        IsBusy = true;
        StatusMessage = "Scanning GTA V folder…";

        try
        {
            var gtaRoot = AppConfig.Instance.GtaPath;
            var scan = await Task.Run(() => LspdfrCleanupScanner.Scan(gtaRoot));
            var mode = BuildMode(SelectedMode);
            Preset = mode.Apply(scan);

            Groups.Clear();
            foreach (var g in Preset.Groups)
                Groups.Add(new SelectableGroup(g, Preset.DefaultSelectedIds));

            StatusMessage = "";
            Step = CleanupStep.Preview;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Back()
    {
        if (Step == CleanupStep.Confirm) { Step = CleanupStep.Preview; return; }
        if (Step == CleanupStep.Preview) { Groups.Clear(); Step = CleanupStep.ModeSelect; }
    }

    private bool CanConfirm()
    {
        if (IsBusy || Step != CleanupStep.Confirm || Preset is null) return false;
        return SelectedCandidates().Count > 0;
    }

    private async Task ApplyAsync()
    {
        if (Preset is null) return;

        IsBusy = true;
        StatusMessage = "Creating backup…";

        try
        {
            var selected = SelectedCandidates();
            var result = await _applyService.ApplyAsync(
                AppConfig.Instance.GtaPath, selected, Preset.Mode);

            ApplyResult = result;
            StatusMessage = result.Success ? "Done." : result.AbortReason ?? "Failed.";
            Step = CleanupStep.Result;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel() => OnCancelled?.Invoke();

    private List<RemovalCandidate> SelectedCandidates() =>
        Groups
            .SelectMany(g => g.Items)
            .Where(s => s.IsSelected && !s.IsBlocked)
            .Select(s => s.Candidate)
            .ToList();

    private static ICleanupMode BuildMode(CleanupMode mode) => mode switch
    {
        CleanupMode.ThirdPartyPluginCleanup => new SelectedThirdPartyPluginCleanupMode(),
        _                                   => new SafeCoreResetMode(),
    };

    // Advance from Preview to Confirm
    public void ProceedToConfirm()
    {
        if (Step != CleanupStep.Preview) return;
        Step = CleanupStep.Confirm;
    }
}
