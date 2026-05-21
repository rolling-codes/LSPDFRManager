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

        var rphExe = LspdfrInstallLocator.FindRagePluginHook(gtaRoot);
        if (rphExe is null)
            findings.Add(new DiagnosticFinding
            {
                Category = "RAGE Plugin Hook",
                Title = "RAGEPluginHook.exe not found.",
                Severity = DiagnosticSeverity.Warning,
                AffectedPath = Path.Combine(gtaRoot, "RAGEPluginHook.exe"),
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

        var lspdfrFolder = LspdfrInstallLocator.FindLspdfrFolder(gtaRoot);
        if (lspdfrFolder is null)
            findings.Add(new DiagnosticFinding
            {
                Category = "LSPDFR",
                Title = "LSPDFR folder not found.",
                Severity = DiagnosticSeverity.Error,
                AffectedPath = Path.Combine(gtaRoot, "lspdfr"),
                Confidence = 1.0f,
            });

        var lspdfrDll = LspdfrInstallLocator.FindLspdfrCore(gtaRoot);
        if (lspdfrDll is null)
            findings.Add(new DiagnosticFinding
            {
                Category = "LSPDFR",
                Title = "LSPDFR core DLL not found.",
                Severity = DiagnosticSeverity.Error,
                AffectedPath = Path.Combine(gtaRoot, "plugins", "LSPD First Response.dll"),
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

            AddUltimateBackupStopThePedWarning(findings, gtaRoot);
            AddPotentialTransportCoronerWarning(findings, gtaRoot);
            AddSpecificKeybindOverlapWarning(findings, conflicts);
            AddOverwriteWithoutBackupWarning(findings);
        }

        findings.AddRange(backupTask.Result);
        findings.AddRange(presetTask.Result);

        return Finalize(findings);
    }

    private static void AddUltimateBackupStopThePedWarning(List<DiagnosticFinding> findings, string gtaRoot)
    {
        var ultimateBackupDll = Path.Combine(gtaRoot, "plugins", "lspdfr", "UltimateBackup.dll");
        var stopThePedDll = Path.Combine(gtaRoot, "plugins", "lspdfr", "StopThePed.dll");

        if (!File.Exists(ultimateBackupDll) || File.Exists(stopThePedDll))
            return;

        findings.Add(new DiagnosticFinding
        {
            Category = "Dependencies",
            Title = "Ultimate Backup installed without Stop The Ped",
            Detail = "Ultimate Backup is installed, but Stop The Ped was not detected. Some Ultimate Backup units, such as Police Transport or Coroner-style integrations, may not work.",
            RecommendedFix = "Install Stop The Ped before enabling transport/coroner-style Ultimate Backup features.",
            AffectedPath = ultimateBackupDll,
            Severity = DiagnosticSeverity.Warning,
            Confidence = 1.0f,
        });
    }

    private static void AddPotentialTransportCoronerWarning(List<DiagnosticFinding> findings, string gtaRoot)
    {
        var stopThePedDll = Path.Combine(gtaRoot, "plugins", "lspdfr", "StopThePed.dll");
        if (File.Exists(stopThePedDll))
            return;

        var candidates = new[]
        {
            Path.Combine(gtaRoot, "plugins", "lspdfr", "UltimateBackup.ini"),
            Path.Combine(gtaRoot, "plugins", "lspdfr", "UltimateBackup", "DefaultRegions.xml"),
            Path.Combine(gtaRoot, "plugins", "lspdfr", "UltimateBackup", "backup.xml"),
            Path.Combine(gtaRoot, "lspdfr", "data", "backup.xml"),
            Path.Combine(gtaRoot, "lspdfr", "data", "agency.xml"),
            Path.Combine(gtaRoot, "lspdfr", "data", "regions.xml"),
            Path.Combine(gtaRoot, "lspdfr", "data", "customregions.xml"),
            Path.Combine(gtaRoot, "lspdfr", "data", "units.xml"),
        };

        foreach (var file in candidates.Where(File.Exists))
        {
            string content;
            try { content = File.ReadAllText(file); }
            catch { continue; }

            if (!InstallerSafetyPolicy.ReferencesTransportOrCoroner(content))
                continue;

            findings.Add(new DiagnosticFinding
            {
                Category = "Config",
                Title = "Ultimate Backup config references Stop The Ped-dependent units",
                Detail = "Ultimate Backup config references units that may require Stop The Ped.",
                RecommendedFix = "Install Stop The Ped or adjust transport/coroner entries before using this backup config.",
                AffectedPath = file,
                Severity = DiagnosticSeverity.Warning,
                Confidence = 0.9f,
            });
        }
    }

    private static void AddSpecificKeybindOverlapWarning(List<DiagnosticFinding> findings, IReadOnlyList<KeybindConflict> conflicts)
    {
        foreach (var conflict in conflicts)
        {
            var groups = conflict.Conflicts
                .Select(entry => ClassifyKeybindOwner(entry.FilePath))
                .Where(group => group is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var includesLspdfr = groups.Contains("LSPDFR", StringComparer.OrdinalIgnoreCase);
            var includesStopThePed = groups.Contains("StopThePed", StringComparer.OrdinalIgnoreCase);
            var includesUltimateBackup = groups.Contains("UltimateBackup", StringComparer.OrdinalIgnoreCase);

            if (!includesLspdfr)
                continue;

            if (!(includesStopThePed || includesUltimateBackup))
                continue;

            findings.Add(new DiagnosticFinding
            {
                Category = "Keybinds",
                Title = "Potential LSPDFR / Stop The Ped / Ultimate Backup keybind overlap",
                Detail = "LSPDFR, Stop The Ped, and Ultimate Backup keybinds may overlap. Review backup/interact keys.",
                Severity = DiagnosticSeverity.Warning,
                Confidence = 1.0f,
            });
            return;
        }
    }

    private static string? ClassifyKeybindOwner(string filePath)
    {
        var file = Path.GetFileName(filePath);
        if (file.Contains("stoptheped", StringComparison.OrdinalIgnoreCase))
            return "StopThePed";
        if (file.Contains("ultimatebackup", StringComparison.OrdinalIgnoreCase)
            || file.Contains("backup", StringComparison.OrdinalIgnoreCase))
            return "UltimateBackup";
        if (file.Contains("lspdfr", StringComparison.OrdinalIgnoreCase))
            return "LSPDFR";
        return null;
    }

    private static void AddOverwriteWithoutBackupWarning(List<DiagnosticFinding> findings)
    {
        var history = ChangeHistoryService.Instance.Entries;
        if (history.Count == 0)
            return;

        var recentWrites = history
            .Where(e => e.Action == ChangeHistoryAction.Installed)
            .Where(e => e.OccurredAt >= DateTime.UtcNow.AddDays(-2))
            .Where(e => !string.IsNullOrWhiteSpace(e.AffectedFile))
            .Where(e => InstallerSafetyPolicy.IsSensitiveOverwriteTarget(Path.GetFileName(e.AffectedFile!)))
            .ToList();

        if (recentWrites.Count == 0)
            return;

        var backupCreated = history
            .Where(e => e.Action == ChangeHistoryAction.BackupCreated)
            .Where(e => e.OccurredAt >= DateTime.UtcNow.AddDays(-2))
            .Select(e => e.AffectedFile)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = recentWrites
            .Where(entry => !backupCreated.Contains(entry.AffectedFile!))
            .ToList();

        if (uncovered.Count == 0)
            return;

        findings.Add(new DiagnosticFinding
        {
            Category = "Config",
            Title = "Backup-related config may have been overwritten without a restorable original",
            Detail = "Backup-related config may have been overwritten without a restorable original.",
            Severity = DiagnosticSeverity.Warning,
            Confidence = 0.7f,
        });
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
