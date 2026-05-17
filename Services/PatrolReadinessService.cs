using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class PatrolReadinessService
{
    private readonly VersionDetectorService _versionDetector = new();

    public async Task<PatrolReadinessResult> CheckAsync()
    {
        var blocking = new List<string>();
        var warnings = new List<string>();
        var passing  = new List<string>();

        var gtaPath = AppConfig.Instance.GtaPath;

        // --- Blocking: GTA path ---
        if (string.IsNullOrWhiteSpace(gtaPath))
        {
            blocking.Add("GTA V path is not configured. Open Settings to set it.");
            return Build(blocking, warnings, passing);
        }

        if (!Directory.Exists(gtaPath))
        {
            blocking.Add($"GTA V directory does not exist: {gtaPath}");
            return Build(blocking, warnings, passing);
        }

        passing.Add("GTA V path is configured and the directory exists.");

        // Run version detection once — shared for all checks below
        VersionBundle? bundle = null;
        try
        {
            bundle = await _versionDetector.DetectAsync(gtaPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            warnings.Add($"Version scan could not complete: {ex.Message}");
        }

        // --- Blocking: required files ---
        if (bundle is null || !bundle.GtaPresent)
            blocking.Add("GTA5.exe was not found in the configured GTA V directory.");
        else
            passing.Add("GTA5.exe found.");

        if (bundle is null || !bundle.RagePluginHookPresent)
            blocking.Add("RAGEPluginHook.exe is missing — LSPDFR cannot launch.");
        else
            passing.Add("RAGEPluginHook.exe found.");

        if (bundle is null || !bundle.LspdfrPresent)
            blocking.Add("LSPDFR core files were not found.");
        else
            passing.Add("LSPDFR core files found.");

        // --- Warnings: optional files ---
        if (bundle is not null)
        {
            if (bundle.ScriptHookVVersion is null)
                warnings.Add("ScriptHookV.dll not found — some mods may not work.");
            else
                passing.Add("ScriptHookV.dll found.");

            if (bundle.ScriptHookVDotNetVersion is null)
                warnings.Add("ScriptHookVDotNet not found — CS/VB script mods will not load.");
            else
                passing.Add("ScriptHookVDotNet found.");

            passing.Add("Version scan completed.");

            // --- GTA drift ---
            var driftMessages = GtaDriftDetector.Detect(bundle, GtaBaselineService.Instance.Current);
            foreach (var msg in driftMessages)
                warnings.Add(msg);
        }

        // --- Transaction warnings ---
        var latestPartial = TransactionService.Instance.Transactions
            .LastOrDefault(t => t.State == TransactionState.PartialRollback);
        if (latestPartial is not null)
            warnings.Add($"Last rollback of \"{latestPartial.ModName}\" was partial — some files may remain.");
        else
            passing.Add("No partial rollback transactions detected.");

        return Build(blocking, warnings, passing);
    }

    private static PatrolReadinessResult Build(
        List<string> blocking,
        List<string> warnings,
        List<string> passing) =>
        new()
        {
            OverallState   = PatrolReadinessResult.ComputeState(blocking, warnings, passing),
            BlockingIssues = blocking,
            Warnings       = warnings,
            PassingChecks  = passing,
        };
}
