using LSPDFRManager.Core;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.Features.SafeMode;

public sealed class SafeModeController : ISafeModeController
{
    private readonly SafeLaunchManager _manager;
    private readonly RestorePointService _restorePoints;

    public SafeModeController(
        SafeLaunchManager? manager = null,
        RestorePointService? restorePoints = null)
    {
        _manager       = manager       ?? new SafeLaunchManager();
        _restorePoints = restorePoints ?? RestorePointService.Instance;
    }

    public Task<SafeLaunchPlan> BuildPreviewAsync(string mode, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var plan = _manager.BuildPlan(mode);
        AppLogger.Info($"[SafeMode] Preview — mode={mode}, changes={plan.Changes.Count}");
        return Task.FromResult(plan);
    }

    public async Task<SafeModeApplyResult> ApplyAsync(
        SafeLaunchPlan plan,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        AppLogger.Info($"[SafeMode] Applying mode={plan.Mode}: {plan.Changes.Count} changes");
        progress?.Report($"Applying Safe Mode ({plan.Mode})…");

        // Backup — create restore point before touching any files
        var rp = new RestorePoint
        {
            OperationName = $"Safe Mode: {plan.Mode}",
            Entries = plan.Changes.Select(c => new RestorePointEntry
            {
                RelativePath = Path.GetRelativePath(AppConfig.Instance.GtaPath, c.FilePath),
                WasEnabled   = c.WasEnabled,
            }).ToList(),
        };
        await _restorePoints.SaveAsync(rp).ConfigureAwait(false);
        AppLogger.Info($"[SafeMode] Restore point saved: {rp.Id}");
        progress?.Report("Restore point created.");

        // Apply
        var failedPaths   = new List<string>();
        var disabledCount = 0;

        foreach (var change in plan.Changes)
        {
            ct.ThrowIfCancellationRequested();
            if (change.WillBeEnabled || change.FilePath.EndsWith(".disabled")) continue;
            try
            {
                File.Move(change.FilePath, change.FilePath + ".disabled");
                disabledCount++;
                progress?.Report($"Disabled: {Path.GetFileName(change.FilePath)}");
                AppLogger.Info($"[SafeMode] Disabled: {change.FilePath}");
            }
            catch (Exception ex)
            {
                failedPaths.Add(change.FilePath);
                progress?.Report($"Failed: {Path.GetFileName(change.FilePath)} — {ex.Message}");
                AppLogger.Error($"[SafeMode] Failed to disable {change.FilePath}", ex);
            }
        }

        // Verify — confirm each expected file is now in its disabled state
        var verified = plan.Changes
            .Where(c => !failedPaths.Contains(c.FilePath) && !c.FilePath.EndsWith(".disabled"))
            .Count(c => File.Exists(c.FilePath + ".disabled"));

        // Report
        var failed  = failedPaths.Count;
        var message = failed == 0
            ? $"Done: {disabledCount} file(s) disabled. Restore point saved (ID {rp.Id[..8]})."
            : $"Applied with {failed} error(s): {disabledCount} disabled. Restore point saved (ID {rp.Id[..8]}).";

        AppLogger.Info($"[SafeMode] Result: disabled={disabledCount}, failed={failed}, verified={verified}");
        ChangeHistoryService.Instance.Record(
            ChangeHistoryAction.SafeLaunchApplied,
            $"Safe Mode applied: {plan.Mode}",
            detail: rp.Id);

        return new SafeModeApplyResult(
            Success:        failed == 0,
            FilesDisabled:  disabledCount,
            FilesFailed:    failed,
            RestorePointId: rp.Id,
            FailedPaths:    failedPaths,
            StatusMessage:  message);
    }
}
