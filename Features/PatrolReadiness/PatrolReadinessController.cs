using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.Features.PatrolReadiness;

/// <summary>
/// Orchestrates the readiness scanners, merges results, and calculates the
/// overall status and score.  No direct file-system calls — delegates to services.
/// </summary>
public sealed class PatrolReadinessController : IPatrolReadinessController
{
    private readonly PatrolReadinessService _coreService;
    private readonly ModHealthScoringService _healthScorer;
    private readonly DllDuplicateScanner _dllScanner;
    private readonly IniLinterService _linter;
    private readonly GtaBaselineService _baseline;
    private readonly ConfigDiscoveryService? _configDiscovery;

    public PatrolReadinessController(
        PatrolReadinessService? coreService = null,
        ModHealthScoringService? healthScorer = null,
        DllDuplicateScanner? dllScanner = null,
        IniLinterService? linter = null,
        GtaBaselineService? baseline = null,
        ConfigDiscoveryService? configDiscovery = null)
    {
        _coreService    = coreService    ?? new PatrolReadinessService();
        _healthScorer   = healthScorer   ?? new ModHealthScoringService();
        _dllScanner     = dllScanner     ?? new DllDuplicateScanner();
        _linter         = linter         ?? new IniLinterService();
        _baseline       = baseline       ?? GtaBaselineService.Instance;

        var gtaPath = AppConfig.Instance.GtaPath;
        _configDiscovery = configDiscovery ??
            (string.IsNullOrEmpty(gtaPath) ? null : new ConfigDiscoveryService(gtaPath));
    }

    public async Task<PatrolReadinessSummary> ScanAsync(CancellationToken ct = default)
    {
        var blocking = new List<InstallIssue>();
        var warnings = new List<InstallIssue>();
        var info     = new List<InstallIssue>();

        // ── 1. Core GTA / RPH / LSPDFR checks ───────────────────────────────
        ct.ThrowIfCancellationRequested();
        var core = await _coreService.CheckAsync().ConfigureAwait(false);

        foreach (var msg in core.BlockingIssues)
            blocking.Add(Issue("core-check", msg, msg, "PatrolReadiness",
                new SuggestedFix("Fix the configuration issue reported above.")));

        foreach (var msg in core.Warnings)
            warnings.Add(Issue("core-warning", msg, msg, "PatrolReadiness"));

        // ── 2. Mod health ────────────────────────────────────────────────────
        ct.ThrowIfCancellationRequested();
        // Score against the current DiagnosticsOrchestrator findings (empty list if not yet scanned)
        var modResults = _healthScorer.ScoreAll([]);
        var brokenMods = modResults.Where(r => r.Status == ModHealthStatus.Broken).ToList();
        var attnMods   = modResults.Where(r => r.Status == ModHealthStatus.NeedsAttention).ToList();

        foreach (var mod in brokenMods)
            blocking.Add(Issue("broken-mod",
                $"Broken mod detected: {ModName(mod.ModId)}",
                string.Join("; ", mod.Issues),
                "ModHealth",
                new SuggestedFix("Disable or reinstall the broken mod.", FixRisk.None)));

        foreach (var mod in attnMods)
            warnings.Add(Issue("mod-needs-attention",
                $"Mod needs attention: {ModName(mod.ModId)}",
                string.Join("; ", mod.Issues),
                "ModHealth"));

        // ── 3. Duplicate DLLs ────────────────────────────────────────────────
        ct.ThrowIfCancellationRequested();
        var dllDups = _dllScanner.Scan();

        foreach (var dup in dllDups)
        {
            var detail = $"Found in: {string.Join(", ", dup.Copies)}";
            var fix = new SuggestedFix(
                "Keep only one copy of this DLL to avoid version conflicts.",
                FixRisk.None);

            if (dup.IsKnownSharedDep)
                warnings.Add(Issue("duplicate-shared-dll",
                    $"Duplicate shared DLL: {dup.DllName} ({dup.Count} copies)",
                    detail, "DllDuplicate", fix));
            else
                info.Add(Issue("duplicate-dll",
                    $"Duplicate DLL: {dup.DllName} ({dup.Count} copies)",
                    detail, "DllDuplicate", fix));
        }

        // ── 4. Config lint ───────────────────────────────────────────────────
        ct.ThrowIfCancellationRequested();
        var lintFindings = new List<LintFinding>();
        if (_configDiscovery is not null)
        {
            var configs = _configDiscovery.DiscoverAll();
            foreach (var cfg in configs)
            {
                if (!_linter.Supports(cfg.AbsolutePath)) continue;
                lintFindings.AddRange(_linter.Lint(cfg.AbsolutePath));
            }
        }

        foreach (var finding in lintFindings)
        {
            var title = $"{Path.GetFileName(finding.FilePath)}: {finding.Message}";
            var fix = new SuggestedFix("Fix the config file issue reported above.");
            switch (finding.Severity)
            {
                case DiagnosticSeverity.Error or DiagnosticSeverity.Critical:
                    blocking.Add(Issue("invalid-config", title, finding.Message, "ConfigLint", fix));
                    break;
                case DiagnosticSeverity.Warning:
                    warnings.Add(Issue("config-warning", title, finding.Message, "ConfigLint", fix));
                    break;
                default:
                    info.Add(Issue("config-info", title, finding.Message, "ConfigLint"));
                    break;
            }
        }

        // ── 5. Known-good diff ───────────────────────────────────────────────
        ct.ThrowIfCancellationRequested();
        var diff = _baseline.DiffCurrentVsKnownGood();
        if (diff is { HasChanges: true })
        {
            if (diff.AddedPlugins.Count > 0)
                warnings.Add(Issue("kg-plugins-added",
                    $"{diff.AddedPlugins.Count} plugin file(s) added since last known-good",
                    string.Join(", ", diff.AddedPlugins.Select(Path.GetFileName)),
                    "KnownGood"));

            if (diff.RemovedPlugins.Count > 0)
                warnings.Add(Issue("kg-plugins-removed",
                    $"{diff.RemovedPlugins.Count} plugin file(s) removed since last known-good",
                    string.Join(", ", diff.RemovedPlugins.Select(Path.GetFileName)),
                    "KnownGood"));

            if (diff.ChangedConfigs.Count > 0)
                info.Add(Issue("kg-configs-changed",
                    $"{diff.ChangedConfigs.Count} config file(s) changed since last known-good",
                    string.Join(", ", diff.ChangedConfigs),
                    "KnownGood"));
        }

        // ── 6. Calculate score and status ────────────────────────────────────
        var score = Math.Max(0, 100 - blocking.Count * 20 - warnings.Count * 5);

        var status = blocking.Count > 0 ? PatrolReadinessState.NotReady
                   : warnings.Count > 0 ? PatrolReadinessState.Warning
                   : PatrolReadinessState.Ready;

        // ── 7. Deduplicated suggested fixes ──────────────────────────────────
        var allFixes = blocking.Concat(warnings)
            .SelectMany(i => i.Fixes)
            .DistinctBy(f => f.Text)
            .Take(5)
            .ToList();

        Core.AppLogger.Info($"[PatrolReadiness] Scan complete — status={status}, score={score}, " +
                            $"blockers={blocking.Count}, warnings={warnings.Count}");

        return new PatrolReadinessSummary(
            Status:              status,
            Score:               score,
            BlockingIssues:      blocking,
            Warnings:            warnings,
            Info:                info,
            SuggestedFixes:      allFixes,
            ScannedAt:           DateTimeOffset.UtcNow,
            CoreChecks:          core,
            ModHealthSummary:    modResults,
            DuplicateDllSummary: dllDups,
            ConfigLintSummary:   lintFindings,
            KnownGoodDiffSummary: diff);
    }

    public void MarkKnownGood()
    {
        _baseline.MarkKnownGood();
        Core.AppLogger.Info("[PatrolReadiness] Marked current state as known-good.");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static InstallIssue Issue(
        string code, string title, string detail, string source,
        params SuggestedFix[] fixes) =>
        new(code, title, detail, source, fixes);

    private static string ModName(Guid modId)
    {
        var mod = ModLibraryService.Instance.Mods.FirstOrDefault(m => m.Id == modId);
        return mod?.Name ?? modId.ToString("N")[..8];
    }
}
