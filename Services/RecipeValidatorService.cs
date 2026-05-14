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
            ExpectedFiles = ["plugins/LSPDFR.dll"],
            ExpectedFolders = ["plugins/lspdfr"],
            Dependencies = ["RAGEPluginHook.exe", "ScriptHookV.dll", "dinput8.dll"],
            HelpText = "LSPDFR.dll must be in the plugins/ folder, not plugins/lspdfr/.",
        },
        new ModTypeRecipe
        {
            Name = "RAGE Plugin Hook",
            Type = ModType.LspdfrPlugin,
            ExpectedFiles = ["RAGEPluginHook.exe"],
            Dependencies = [],
            HelpText = "RAGEPluginHook.exe must be in the GTA V root.",
        },
        new ModTypeRecipe
        {
            Name = "RageNativeUI",
            Type = ModType.LspdfrPlugin,
            ExpectedFiles = ["plugins/RageNativeUI.dll"],
            CommonWrongPaths = ["RageNativeUI.dll", "plugins/lspdfr/RageNativeUI.dll", "scripts/RageNativeUI.dll"],
            HelpText = "RageNativeUI.dll belongs in the plugins/ folder, not plugins/lspdfr/ or GTA root.",
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
            HelpText = "ScriptHookV.dll and dinput8.dll must be in the GTA V root.",
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
        "LSPDFR Core", "RAGE Plugin Hook", "ScriptHookV", "ELS", "AdvancedHookV", "OpenIV.asi",
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

        return findings;
    }

    // A. Missing expected files
    private void CheckMissingFiles(ModTypeRecipe recipe, List<DiagnosticFinding> findings)
    {
        var isErrorRecipe = ErrorOnMissingRecipes.Contains(recipe.Name);

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

    private bool FileOrDirExists(string relPath)
    {
        var full = Path.Combine(_gtaPath, NormalizeSep(relPath));
        return File.Exists(full) || Directory.Exists(full);
    }

    private static string NormalizeSep(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);
}
