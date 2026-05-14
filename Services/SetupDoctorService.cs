using System.Diagnostics;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class SetupDoctorService
{
    public async Task<IReadOnlyList<DiagnosticFinding>> RunAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<DiagnosticFinding>();
        var gtaRoot = AppConfig.Instance.GtaPath;

        if (string.IsNullOrWhiteSpace(gtaRoot) || !Directory.Exists(gtaRoot))
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "GTA path is not configured or does not exist",
                Detail = $"Configured path: \"{gtaRoot}\"",
                Severity = DiagnosticSeverity.Error,
                Confidence = 1.0f,
            });
            return Finalize(findings);
        }

        if (gtaRoot.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "GTA V installed in OneDrive path. This may cause file locking and update issues.",
                Severity = DiagnosticSeverity.Warning,
                AffectedPath = gtaRoot,
                Confidence = 1.0f,
            });

        if (gtaRoot.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "GTA V installed in Program Files. Write permission issues are common here.",
                Severity = DiagnosticSeverity.Warning,
                AffectedPath = gtaRoot,
                Confidence = 1.0f,
            });

        try
        {
            var probe = Path.Combine(gtaRoot, $".lspm_write_check_{Guid.NewGuid():N}.tmp");
            using (var fs = File.OpenWrite(probe)) { }
            File.Delete(probe);
        }
        catch (UnauthorizedAccessException)
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "GTA V folder is not writable.",
                Severity = DiagnosticSeverity.Error,
                AffectedPath = gtaRoot,
                Confidence = 1.0f,
            });
        }

        var gta5Exe = Path.Combine(gtaRoot, "GTA5.exe");
        if (File.Exists(gta5Exe))
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(gta5Exe);
                var actualVersion = fvi.FileVersion ?? fvi.ProductVersion ?? "";
                var lastKnown = AppConfig.Instance.LastKnownGameVersion;
                if (!string.IsNullOrWhiteSpace(lastKnown) &&
                    !string.Equals(lastKnown, actualVersion, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new DiagnosticFinding
                    {
                        Category = "Setup",
                        Title = "GTA V version may have changed since last scan. Some mods may need updating.",
                        Detail = $"Last known: {lastKnown}, Current: {actualVersion}",
                        Severity = DiagnosticSeverity.Warning,
                        AffectedPath = gta5Exe,
                        Confidence = 1.0f,
                    });
                }
            }
            catch (Exception ex)
            {
                Core.AppLogger.Warning($"SetupDoctorService: could not read GTA5.exe version: {ex.Message}");
            }
        }

        var rphExe = Path.Combine(gtaRoot, "RAGEPluginHook.exe");
        if (!File.Exists(rphExe))
            findings.Add(new DiagnosticFinding
            {
                Category = "RAGE Plugin Hook",
                Title = "RAGEPluginHook.exe not found in GTA V root.",
                Severity = DiagnosticSeverity.Warning,
                AffectedPath = rphExe,
                Confidence = 1.0f,
            });

        var rphLog = Path.Combine(gtaRoot, "RagePluginHook.log");
        if (File.Exists(rphLog))
            findings.Add(new DiagnosticFinding
            {
                Category = "RAGE Plugin Hook",
                Title = "RagePluginHook.log found.",
                Detail = rphLog,
                Severity = DiagnosticSeverity.Info,
                AffectedPath = rphLog,
                Confidence = 1.0f,
            });

        var lspdfrFolder = Path.Combine(gtaRoot, "plugins", "lspdfr");
        if (!Directory.Exists(lspdfrFolder))
            findings.Add(new DiagnosticFinding
            {
                Category = "LSPDFR",
                Title = "plugins/lspdfr folder not found.",
                Severity = DiagnosticSeverity.Error,
                AffectedPath = lspdfrFolder,
                Confidence = 1.0f,
            });

        var lspdfrDll = Path.Combine(gtaRoot, "plugins", "LSPDFR.dll");
        if (!File.Exists(lspdfrDll))
            findings.Add(new DiagnosticFinding
            {
                Category = "LSPDFR",
                Title = "plugins/LSPDFR.dll not found.",
                Severity = DiagnosticSeverity.Error,
                AffectedPath = lspdfrDll,
                Confidence = 1.0f,
            });

        cancellationToken.ThrowIfCancellationRequested();

        var recipeTask = Task.Run(() =>
        {
            try { return new RecipeValidatorService(gtaRoot).Validate(); }
            catch (Exception ex)
            {
                Core.AppLogger.Warning($"SetupDoctorService: RecipeValidatorService failed: {ex.Message}");
                return [new DiagnosticFinding
                {
                    Category = "Setup",
                    Title = "Recipe validator could not run.",
                    Detail = ex.Message,
                    Severity = DiagnosticSeverity.Info,
                    Confidence = 1.0f,
                }];
            }
        }, cancellationToken);

        var configTask = Task.Run(() =>
        {
            try
            {
                var configs = new ConfigDiscoveryService(gtaRoot).DiscoverAll();
                var conflicts = new KeybindConflictScanner().Scan(configs);
                return (configs, conflicts, error: (string?)null);
            }
            catch (Exception ex)
            {
                Core.AppLogger.Warning($"SetupDoctorService: Config/Keybind scan failed: {ex.Message}");
                return (configs: (List<DiscoveredConfig>?)null, conflicts: (List<KeybindConflict>?)null, error: ex.Message);
            }
        }, cancellationToken);

        var backupTask = Task.Run(() =>
        {
            try { return new BackupEasyEditorService(gtaRoot).ValidateBackupConfigs(); }
            catch (Exception ex)
            {
                Core.AppLogger.Warning($"SetupDoctorService: BackupEasyEditorService failed: {ex.Message}");
                return [new DiagnosticFinding
                {
                    Category = "Setup",
                    Title = "Backup config validator could not run.",
                    Detail = ex.Message,
                    Severity = DiagnosticSeverity.Info,
                    Confidence = 1.0f,
                }];
            }
        }, cancellationToken);

        var presetTask = Task.Run(() =>
        {
            var presetFindings = new List<DiagnosticFinding>();
            var svc = new PresetPatchService(gtaRoot);
            foreach (var preset in PresetPatchService.BuiltInPresets)
            {
                try
                {
                    var previews = svc.Preview(preset);
                    var hasUnfulfilled = preset.Rules.Any(rule =>
                    {
                        var absPath = Path.Combine(gtaRoot, rule.File.Replace('/', Path.DirectorySeparatorChar));
                        return !File.Exists(absPath);
                    });
                    if (hasUnfulfilled)
                        presetFindings.Add(new DiagnosticFinding
                        {
                            Category = "Presets",
                            Title = $"Preset '{preset.DisplayName}' cannot be fully applied: target file not found",
                            Severity = DiagnosticSeverity.Warning,
                            Confidence = 1.0f,
                        });
                }
                catch (Exception ex)
                {
                    Core.AppLogger.Warning($"SetupDoctorService: Preset preview failed for '{preset.DisplayName}': {ex.Message}");
                    presetFindings.Add(new DiagnosticFinding
                    {
                        Category = "Presets",
                        Title = $"Preset '{preset.DisplayName}' check could not run.",
                        Detail = ex.Message,
                        Severity = DiagnosticSeverity.Info,
                        Confidence = 1.0f,
                    });
                }
            }
            return presetFindings;
        }, cancellationToken);

        await Task.WhenAll(recipeTask, configTask, backupTask, presetTask);

        findings.AddRange(recipeTask.Result);

        var (configs, conflicts, configError) = configTask.Result;
        if (configError != null)
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "Config/Keybind scanner could not run.",
                Detail = configError,
                Severity = DiagnosticSeverity.Info,
                Confidence = 1.0f,
            });
        }
        else
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Config",
                Title = $"Discovered {configs!.Count} config files under GTA root",
                Severity = DiagnosticSeverity.Info,
                Confidence = 1.0f,
            });
            foreach (var conflict in conflicts!)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Keybinds",
                    Title = $"Keybind conflict: '{conflict.KeyValue}' used in {conflict.Conflicts.Length} places",
                    Detail = string.Join(", ", conflict.Conflicts.Select(c => $"{c.FilePath}[{c.Section}]/{c.Key}")),
                    Severity = conflict.Severity,
                    Confidence = 1.0f,
                });
            }
        }

        findings.AddRange(backupTask.Result);
        findings.AddRange(presetTask.Result);

        return Finalize(findings);
    }

    private static IReadOnlyList<DiagnosticFinding> Finalize(List<DiagnosticFinding> findings)
    {
        var deduped = Deduplicate(findings);
        return NormalizeConfidence(deduped);
    }

    internal static List<DiagnosticFinding> Deduplicate(List<DiagnosticFinding> findings)
    {
        var seen = new HashSet<(string, string?, DiagnosticSeverity)>();
        var result = new List<DiagnosticFinding>(findings.Count);
        foreach (var f in findings)
        {
            if (seen.Add((f.Title, f.AffectedPath, f.Severity)))
                result.Add(f);
        }
        return result;
    }

    internal static List<DiagnosticFinding> NormalizeConfidence(List<DiagnosticFinding> findings)
    {
        return findings.Select(f => f.Confidence == 0.0f
            ? new DiagnosticFinding
            {
                Category = f.Category,
                Title = f.Title,
                Detail = f.Detail,
                RecommendedFix = f.RecommendedFix,
                AffectedPath = f.AffectedPath,
                Severity = f.Severity,
                AutoFixId = f.AutoFixId,
                Confidence = 1.0f,
            }
            : f).ToList();
    }
}
