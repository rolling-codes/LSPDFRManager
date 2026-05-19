using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class RecipeValidatorService
{
    private readonly string _gtaPath;

    private static readonly ModTypeRecipe[] Recipes =
    [
        new ModTypeRecipe
        {
            Name = "LSPDFR Core",
            Type = ModType.LspdfrPlugin,
            ExpectedFiles = ["plugins/LSPD First Response.dll"],
            ExpectedFolders = ["lspdfr"],
            Dependencies = ["RAGEPluginHook.exe", "ScriptHookV.dll", "dinput8.dll"],
            CommonWrongPaths = ["plugins/LSPDFR.dll", "plugins/lspdfr/LSPDFR.dll"],
            HelpText = "LSPDFR core is normally plugins/LSPD First Response.dll with the lspdfr/ support folder in the GTA V root.",
        },
        new ModTypeRecipe
        {
            Name = "RAGE Plugin Hook",
            Type = ModType.LspdfrPlugin,
            ExpectedFiles = ["RAGEPluginHook.exe", "RagePluginHook.dll"],
            Dependencies = [],
            HelpText = "RAGEPluginHook.exe and RagePluginHook.dll must be in the GTA V root.",
        },
        new ModTypeRecipe
        {
            Name = "RageNativeUI",
            Type = ModType.LspdfrPlugin,
            ExpectedFiles = ["plugins/RageNativeUI.dll"],
            CommonWrongPaths = ["plugins/lspdfr/RageNativeUI.dll", "scripts/RageNativeUI.dll"],
            HelpText = "RageNativeUI.dll belongs in the GTA V root (per the RAGENativeUI author); " +
                       "the plugins/ folder is also accepted. Not plugins/lspdfr/ or scripts/.",
        },
        new ModTypeRecipe
        {
            Name = "Ultimate Backup",
            Type = ModType.CalloutPack,
            ExpectedFiles = ["plugins/lspdfr/UltimateBackup.dll"],
            ConfigFiles = ["plugins/lspdfr/UltimateBackup.ini"],
            HelpText = "Ultimate Backup requires UltimateBackup.ini. Without it, preset patching cannot be applied.",
        },
        new ModTypeRecipe
        {
            Name = "Stop The Ped",
            Type = ModType.CalloutPack,
            ExpectedFiles = ["plugins/lspdfr/StopThePed.dll"],
            ConfigFiles = ["plugins/lspdfr/StopThePed.ini"],
            HelpText = "StopThePed.ini is required for keybind conflict detection.",
        },
        new ModTypeRecipe
        {
            Name = "ELS",
            Type = ModType.AsiMod,
            ExpectedFiles = ["ELS.asi", "ELS.ini", "AdvancedHookV.dll"],
            ExpectedFolders = ["ELS"],
            Dependencies = ["ScriptHookV.dll", "dinput8.dll"],
            CommonWrongPaths = ["plugins/ELS.asi", "plugins/lspdfr/ELS.asi", "scripts/ELS.asi", "plugins/ELS.ini", "plugins/lspdfr/ELS.ini"],
            HelpText = "ELS.asi, ELS.ini, AdvancedHookV.dll, and the ELS/ folder must be in the GTA V root.",
        },
        new ModTypeRecipe
        {
            Name = "AdvancedHookV",
            Type = ModType.AsiMod,
            ExpectedFiles = ["AdvancedHookV.dll"],
            HelpText = "AdvancedHookV.dll must be in the GTA V root alongside ELS.",
        },
        new ModTypeRecipe
        {
            Name = "ScriptHookV",
            Type = ModType.AsiMod,
            ExpectedFiles = ["ScriptHookV.dll", "dinput8.dll"],
            HelpText = "ScriptHookV.dll and dinput8.dll go in the GTA V root. They are only " +
                       "needed for ASI/script mods — RagePluginHook and LSPDFR do not require them.",
        },
        new ModTypeRecipe
        {
            Name = "ScriptHookVDotNet",
            Type = ModType.Script,
            ExpectedFiles = ["ScriptHookVDotNet.asi"],
            Dependencies = ["ScriptHookV.dll"],
            HelpText = "ScriptHookVDotNet.asi must be in the GTA V root.",
        },
        new ModTypeRecipe
        {
            Name = "OpenIV.asi",
            Type = ModType.AsiMod,
            ExpectedFiles = ["OpenIV.asi"],
            HelpText = "OpenIV.asi must be in the GTA V root.",
        },
    ];

    // Recipes where missing files are Errors (not just Warnings)
    private static readonly HashSet<string> ErrorOnMissingRecipes =
    [
        // NOTE: "ScriptHookV" is intentionally NOT an error recipe. ScriptHookV.dll /
        // dinput8.dll are only needed for ASI/script mods; RagePluginHook and LSPDFR do
        // not require them (ragepluginhook.net/Requirements). A missing ScriptHookV is a
        // Warning, not a blocking Error, so a valid RPH/LSPDFR install isn't flagged.
        "LSPDFR Core", "RAGE Plugin Hook", "ELS", "AdvancedHookV", "OpenIV.asi",
    ];

    public RecipeValidatorService(string gtaPath)
    {
        _gtaPath = gtaPath;
    }

    public List<DiagnosticFinding> Validate()
    {
        var findings = new List<DiagnosticFinding>();

        if (string.IsNullOrWhiteSpace(_gtaPath) || !Directory.Exists(_gtaPath))
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Setup",
                Title = "GTA path is not configured or does not exist",
                Detail = $"Configured path: \"{_gtaPath}\"",
                Severity = DiagnosticSeverity.Error,
                Confidence = 1.0f,
            });
            return findings;
        }

        foreach (var recipe in Recipes)
        {
            CheckMissingFiles(recipe, findings);
            CheckMissingFolders(recipe, findings);
            CheckWrongPaths(recipe, findings);
            CheckDuplicates(recipe, findings);
            CheckMissingConfig(recipe, findings);
        }

        AddUltimateBackupDependencyFindings(findings);

        return findings;
    }

    // A. Missing expected files
    private void CheckMissingFiles(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        var isErrorRecipe = ErrorOnMissingRecipes.Contains(recipe.Name);

        if (recipe.Name == "LSPDFR Core" && LspdfrInstallLocator.FindLspdfrCore(_gtaPath) is not null)
            return;

        if (recipe.Name == "RageNativeUI" && RageNativeUiSatisfied())
            return;

        foreach (var relPath in recipe.ExpectedFiles)
        {
            bool exists;
            try { exists = FileOrDirExists(relPath); }
            catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking {relPath}: {ex.Message}"); continue; }

            if (!exists)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Dependencies",
                    Title = $"{recipe.Name} file missing",
                    Detail = $"Expected {relPath}, file not found.",
                    RecommendedFix = recipe.HelpText,
                    AffectedPath = Path.Combine(_gtaPath, NormalizeSep(relPath)),
                    Severity = isErrorRecipe ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                    Confidence = 1.0f,
                });
            }
        }
    }

    // B. Missing expected folders
    private void CheckMissingFolders(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        if (recipe.Name == "LSPDFR Core" && LspdfrInstallLocator.FindLspdfrFolder(_gtaPath) is not null)
            return;

        foreach (var relFolder in recipe.ExpectedFolders)
        {
            bool exists;
            try { exists = Directory.Exists(Path.Combine(_gtaPath, NormalizeSep(relFolder))); }
            catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking folder {relFolder}: {ex.Message}"); continue; }

            if (!exists)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Install",
                    Title = $"{recipe.Name} folder missing",
                    Detail = $"Expected folder {relFolder} was not found.",
                    RecommendedFix = recipe.HelpText,
                    AffectedPath = Path.Combine(_gtaPath, NormalizeSep(relFolder)),
                    Severity = DiagnosticSeverity.Warning,
                    Confidence = 1.0f,
                });
            }
        }
    }

    // C. Files found at common wrong paths (and NOT at correct path)
    private void CheckWrongPaths(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        if (recipe.CommonWrongPaths.Length == 0) return;

        // Mirror the CheckMissingFiles guard: plugins/LSPDFR.dll and plugins/lspdfr/LSPDFR.dll
        // are tolerated legacy aliases for the LSPDFR core (LspdfrInstallLocator), not
        // wrong-folder errors. If any accepted core is present, do not flag this recipe.
        if (recipe.Name == "LSPDFR Core" && LspdfrInstallLocator.FindLspdfrCore(_gtaPath) is not null)
            return;

        // RageNativeUI.dll is canonical in the GTA V root (per the RAGENativeUI author)
        // and also accepted under plugins/. If it is in either accepted location, do not
        // flag any wrong-folder finding for this recipe.
        if (recipe.Name == "RageNativeUI" && RageNativeUiSatisfied())
            return;

        foreach (var wrongRel in recipe.CommonWrongPaths)
        {
            bool atWrong;
            try { atWrong = FileOrDirExists(wrongRel); }
            catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking wrong path {wrongRel}: {ex.Message}"); continue; }

            if (!atWrong) continue;

            // Find the expected correct path for the same filename
            var fileName = Path.GetFileName(wrongRel);
            var correctRel = recipe.ExpectedFiles.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

            bool atCorrect = false;
            if (correctRel != null)
            {
                try { atCorrect = FileOrDirExists(correctRel); }
                catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking correct path {correctRel}: {ex.Message}"); continue; }
            }

            if (!atCorrect)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Install",
                    Title = $"{recipe.Name} installed in the wrong folder",
                    Detail = $"Found {wrongRel}, expected {correctRel ?? "GTA root"}.",
                    RecommendedFix = recipe.HelpText,
                    AffectedPath = Path.Combine(_gtaPath, NormalizeSep(wrongRel)),
                    Severity = DiagnosticSeverity.Error,
                    Confidence = 0.9f,
                });
            }
        }
    }

    // D. Duplicate DLLs (correct path AND at least one wrong path)
    private void CheckDuplicates(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        if (recipe.CommonWrongPaths.Length == 0) return;

        foreach (var correctRel in recipe.ExpectedFiles)
        {
            bool atCorrect;
            try { atCorrect = FileOrDirExists(correctRel); }
            catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking {correctRel}: {ex.Message}"); continue; }

            if (!atCorrect) continue;

            var fileName = Path.GetFileName(correctRel);
            var duplicatePaths = new List<string> { correctRel };

            foreach (var wrongRel in recipe.CommonWrongPaths)
            {
                if (!string.Equals(Path.GetFileName(wrongRel), fileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool atWrong;
                try { atWrong = FileOrDirExists(wrongRel); }
                catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking {wrongRel}: {ex.Message}"); continue; }

                if (atWrong) duplicatePaths.Add(wrongRel);
            }

            if (duplicatePaths.Count > 1)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Install",
                    Title = $"{recipe.Name} duplicate detected",
                    Detail = $"Found at multiple paths: {string.Join(", ", duplicatePaths)}.",
                    RecommendedFix = "Keep the version required by the installed plugin set and remove duplicates after backup.",
                    AffectedPath = _gtaPath,
                    Severity = DiagnosticSeverity.Warning,
                    Confidence = 0.8f,
                });
            }
        }
    }

    // E. Config (INI) missing when DLL is present
    private void CheckMissingConfig(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        if (recipe.ConfigFiles.Length == 0) return;

        foreach (var dllRel in recipe.ExpectedFiles)
        {
            bool dllExists;
            try { dllExists = FileOrDirExists(dllRel); }
            catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking {dllRel}: {ex.Message}"); continue; }

            if (!dllExists) continue;

            foreach (var iniRel in recipe.ConfigFiles)
            {
                bool iniExists;
                try { iniExists = FileOrDirExists(iniRel); }
                catch (IOException ex) { Core.AppLogger.Warning($"RecipeValidator: IO error checking {iniRel}: {ex.Message}"); continue; }

                if (!iniExists)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        Category = "Config",
                        Title = $"{recipe.Name} config missing",
                        Detail = $"{dllRel} detected but {iniRel} was not found.",
                        RecommendedFix = recipe.HelpText,
                        AffectedPath = Path.Combine(_gtaPath, NormalizeSep(iniRel)),
                        Severity = DiagnosticSeverity.Warning,
                        Confidence = 1.0f,
                    });
                }
            }
        }
    }

    private void AddUltimateBackupDependencyFindings(List<DiagnosticFinding> findings)
    {
        var ultimateBackupDll = Path.Combine(_gtaPath, NormalizeSep("plugins/lspdfr/UltimateBackup.dll"));
        var stopThePedDll = Path.Combine(_gtaPath, NormalizeSep("plugins/lspdfr/StopThePed.dll"));

        if (!File.Exists(ultimateBackupDll))
            return;

        if (!File.Exists(stopThePedDll))
        {
            findings.Add(new DiagnosticFinding
            {
                Category = "Dependencies",
                Title = "Ultimate Backup detected without Stop The Ped",
                Detail = InstallerSafetyPolicy.GetUltimateBackupMissingStpWarning(),
                RecommendedFix = "Install Stop The Ped before enabling Ultimate Backup transport/coroner integrations.",
                AffectedPath = ultimateBackupDll,
                Severity = DiagnosticSeverity.Warning,
                Confidence = 1.0f,
            });
        }

        if (File.Exists(stopThePedDll))
            return;

        var configCandidates = new[]
        {
            Path.Combine(_gtaPath, NormalizeSep("plugins/lspdfr/UltimateBackup.ini")),
            Path.Combine(_gtaPath, NormalizeSep("plugins/lspdfr/UltimateBackup/DefaultRegions.xml")),
            Path.Combine(_gtaPath, NormalizeSep("plugins/lspdfr/UltimateBackup/backup.xml")),
            Path.Combine(_gtaPath, NormalizeSep("lspdfr/data/backup.xml")),
            Path.Combine(_gtaPath, NormalizeSep("lspdfr/data/agency.xml")),
            Path.Combine(_gtaPath, NormalizeSep("lspdfr/data/regions.xml")),
            Path.Combine(_gtaPath, NormalizeSep("lspdfr/data/customregions.xml")),
            Path.Combine(_gtaPath, NormalizeSep("lspdfr/data/units.xml")),
        };

        foreach (var file in configCandidates.Where(File.Exists))
        {
            string content;
            try { content = File.ReadAllText(file); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            if (!InstallerSafetyPolicy.ReferencesTransportOrCoroner(content))
                continue;

            findings.Add(new DiagnosticFinding
            {
                Category = "Config",
                Title = "Ultimate Backup config may require Stop The Ped",
                Detail = "Ultimate Backup config references units that may require Stop The Ped.",
                RecommendedFix = "Install Stop The Ped or adjust the transport/coroner entries in the backup configuration.",
                AffectedPath = file,
                Severity = DiagnosticSeverity.Warning,
                Confidence = 0.9f,
            });
        }
    }

    private bool FileOrDirExists(string relPath)
    {
        var full = Path.Combine(_gtaPath, NormalizeSep(relPath));
        return File.Exists(full) || Directory.Exists(full);
    }

    private static string NormalizeSep(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    // RAGENativeUI's author documents the GTA V root as canonical; real-world plugin
    // packages also ship it under plugins/. Treat the recipe as satisfied if the DLL
    // is in either accepted location (not plugins/lspdfr/ or scripts/).
    private bool RageNativeUiSatisfied() =>
        FileOrDirExists("RageNativeUI.dll") || FileOrDirExists("plugins/RageNativeUI.dll");
}
