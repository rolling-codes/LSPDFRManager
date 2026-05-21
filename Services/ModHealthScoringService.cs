using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Aggregates existing diagnostic findings and scanner results into a per-mod
/// health badge.  Stateless — all inputs supplied via parameters.
/// </summary>
public sealed class ModHealthScoringService
{
    /// <summary>
    /// Score a single mod against a flat list of diagnostic findings.
    /// Findings are matched by file name or plugin name appearing in the finding title/detail.
    /// </summary>
    public ModHealthResult Score(InstalledMod mod, IReadOnlyList<DiagnosticFinding> findings)
    {
        var issues = new List<string>();
        var worstSeverity = DiagnosticSeverity.Ok;

        foreach (var finding in findings)
        {
            if (!AffectsMod(finding, mod)) continue;
            issues.Add(finding.Title);
            if (finding.Severity > worstSeverity)
                worstSeverity = finding.Severity;
        }

        var status = worstSeverity switch
        {
            DiagnosticSeverity.Critical or DiagnosticSeverity.Error => ModHealthStatus.Broken,
            DiagnosticSeverity.Warning                              => ModHealthStatus.NeedsAttention,
            DiagnosticSeverity.Info                                 => ModHealthStatus.NeedsAttention,
            _                                                       => ModHealthStatus.Healthy,
        };

        if (!mod.IsEnabled)
            status = ModHealthStatus.Unknown;

        return new ModHealthResult(mod.Id, status, issues);
    }

    /// <summary>Score all mods in the library.</summary>
    public IReadOnlyList<ModHealthResult> ScoreAll(IReadOnlyList<DiagnosticFinding> findings)
    {
        var mods = ModLibraryService.Instance.Mods;
        return mods.Select(m => Score(m, findings)).ToList();
    }

    private static bool AffectsMod(DiagnosticFinding finding, InstalledMod mod)
    {
        // Match by install path prefix
        if (!string.IsNullOrEmpty(finding.AffectedPath) &&
            !string.IsNullOrEmpty(mod.InstallPath) &&
            finding.AffectedPath.StartsWith(mod.InstallPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Match by mod name appearing in finding title or detail
        var name = mod.Name;
        if (!string.IsNullOrEmpty(name))
        {
            if (finding.Title.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
            if (finding.Detail?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        // Match by installed file names
        foreach (var file in mod.InstalledFiles)
        {
            var fileName = Path.GetFileName(file);
            if (!string.IsNullOrEmpty(finding.AffectedPath) &&
                Path.GetFileName(finding.AffectedPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
