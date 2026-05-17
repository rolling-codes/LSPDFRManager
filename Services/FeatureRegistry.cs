using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>Canonical list of all app features. Edit here to add/remove a feature.</summary>
public static class FeatureRegistry
{
    public static IReadOnlyList<FeatureManifest> All { get; } =
    [
        new("patrol-readiness",        "Patrol Readiness",           "Pre-flight check before launching LSPDFR.",                FeatureStage.Stable),
        new("smart-zip-installer",     "Smart Archive Installer",    "Classify, preview, and safely install mod archives.",      FeatureStage.Stable),
        new("dependency-checker",      "Dependency Checker",         "Detect missing or duplicate shared DLLs.",                 FeatureStage.Stable),
        new("crash-timeline",          "Crash Timeline",             "Correlate RPH log entries into a crash timeline.",         FeatureStage.Preview),
        new("rollback-center",         "Rollback Center",            "Browse backup history and restore previous states.",       FeatureStage.Stable),
        new("safe-mode-builder",       "Safe Mode Builder",          "Launch GTA V with a minimal plugin set.",                  FeatureStage.Stable),
        new("developer-diagnostics",   "Developer Diagnostics",      "Internal page showing feature flags and rule results.",    FeatureStage.DevOnly),
        new("support-bundle-export",   "Support Bundle Export",      "Package sanitized logs and diagnostics into a ZIP.",       FeatureStage.Preview),
        new("config-linter",           "Config Linter",              "Detect duplicate keys and bad values in .ini/.xml/.json.", FeatureStage.Preview),
        new("keybind-conflict-scan",   "Keybind Conflict Scanner",   "Warn about duplicate hotkeys across plugin configs.",      FeatureStage.Stable),
        new("mod-health-score",        "Mod Health Score",           "Badge each mod with a Healthy / Needs Attention / Broken status.", FeatureStage.Experimental),
        new("quarantine-folder",       "Quarantine Folder",          "Move broken plugins out of the active folder safely.",     FeatureStage.Experimental),
    ];
}
