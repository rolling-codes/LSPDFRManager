using LSPDFRManager.Domain;

namespace LSPDFRManager.Features.SafeMode;

/// <summary>
/// Orchestrates the Preview → Backup → Apply → Verify → Log → Report pipeline
/// for safe-mode file operations.  BuildPreviewAsync is pure (no side effects);
/// ApplyAsync is the only mutating step.
/// </summary>
public interface ISafeModeController
{
    Task<SafeLaunchPlan> BuildPreviewAsync(string mode, CancellationToken ct = default);
    Task<SafeModeApplyResult> ApplyAsync(SafeLaunchPlan plan, IProgress<string>? progress = null, CancellationToken ct = default);
}
