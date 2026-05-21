using System.Xml.Linq;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class BackupEasyEditorService
{
    private readonly string _gtaPath;

    public BackupEasyEditorService(string gtaPath) => _gtaPath = gtaPath;

    public List<DiagnosticFinding> ValidateBackupConfigs()
    {
        var findings = new List<DiagnosticFinding>();
        try
        {
            var files = new BackupConfigDiscoveryService(_gtaPath).DiscoverBackupXmlFiles();

            if (files.Count == 0)
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Config",
                    Title = "No backup XML files found under plugins/lspdfr",
                    Severity = DiagnosticSeverity.Info,
                });
                return findings;
            }

            var allUnits = new List<BackupUnitDefinition>();

            foreach (var file in files)
            {
                var rel = RelativePath(file);
                findings.Add(new DiagnosticFinding
                {
                    Category = "Config",
                    Title = $"Backup config file found: {rel}",
                    Severity = DiagnosticSeverity.Info,
                    AffectedPath = file,
                });

                var diag = BackupXmlParser.Diagnose(file);
                if (diag != null) findings.Add(diag);

                var units = BackupXmlParser.Parse(file);
                if (units.Count == 0 && diag == null)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        Category = "Config",
                        Title = "Backup XML parsed but contained no recognizable unit entries.",
                        Severity = DiagnosticSeverity.Warning,
                        AffectedPath = file,
                    });
                }

                foreach (var unit in units)
                {
                    if (unit.UniformName == null && unit.PedModel == null)
                        findings.Add(new DiagnosticFinding
                        {
                            Category = "Config",
                            Title = "Backup unit has no uniform component data",
                            Severity = DiagnosticSeverity.Info,
                            AffectedPath = file,
                        });
                }

                allUnits.AddRange(units);
            }

            if (EupAppears() && allUnits.All(u => u.UniformName == null))
            {
                findings.Add(new DiagnosticFinding
                {
                    Category = "Config",
                    Title = "EUP appears installed, but backup configs do not include EUP-style uniform data.",
                    Severity = DiagnosticSeverity.Warning,
                });
            }
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"BackupEasyEditorService.ValidateBackupConfigs error: {ex.Message}");
        }
        return findings;
    }

    public BackupUniformPatchPreview PreviewUniformPatch(string xmlFilePath, BackupUniformMapping mapping)
    {
        string[] rawLines;
        try
        {
            rawLines = File.ReadAllLines(xmlFilePath);
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"BackupEasyEditorService.PreviewUniformPatch read error: {ex.Message}");
            return new BackupUniformPatchPreview
            {
                SourceFile = xmlFilePath,
                MappingName = mapping.DisplayName,
                CanApply = false,
                Warnings = [$"Could not read file: {ex.Message}"],
            };
        }

        var before = rawLines
            .Where(l => l.Contains(mapping.Agency, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (before.Length == 0)
        {
            return new BackupUniformPatchPreview
            {
                SourceFile = xmlFilePath,
                MappingName = mapping.DisplayName,
                CanApply = false,
                Warnings = [$"No entries found matching agency '{mapping.Agency}'"],
            };
        }

        var after = before.Select(l =>
        {
            if (l.Contains("UniformName", StringComparison.OrdinalIgnoreCase))
                return l; // already has it — no change shown
            // Insert before closing /> or >
            if (l.TrimEnd().EndsWith("/>"))
                return l.TrimEnd()[..^2] + $" UniformName=\"{mapping.PlayerOutfitName ?? mapping.DisplayName}\" />";
            return l + $" <!-- UniformName=\"{mapping.PlayerOutfitName ?? mapping.DisplayName}\" -->";
        }).ToArray();

        return new BackupUniformPatchPreview
        {
            SourceFile = xmlFilePath,
            MappingName = mapping.DisplayName,
            BeforeLines = before,
            AfterLines = after,
            CanApply = true,
        };
    }

    // ── EUP assignment methods ────────────────────────────────────────────────

    public List<EupUniformDefinition> GetEupUniforms(
        string? department = null,
        string? county = null,
        EupGender? gender = null)
    {
        var svc = new EupOutfitDiscoveryService(_gtaPath);
        return svc.Discover()
            .Where(u => IsAny(department) ||
                        u.Department.Equals(department ?? "", StringComparison.OrdinalIgnoreCase))
            .Where(u => IsAny(county) ||
                        u.County.Equals(county ?? "", StringComparison.OrdinalIgnoreCase))
            .Where(u => gender is null || gender == EupGender.Any ||
                        u.Gender == gender || u.Gender == EupGender.Unknown)
            .ToList();
    }

    public List<BackupUnitDefinition> GetBackupUnits(
        string? department = null,
        string? county = null,
        EupGender? gender = null,
        string? category = null)
    {
        var files = new BackupConfigDiscoveryService(_gtaPath).DiscoverBackupXmlFiles();
        var units = files.SelectMany(BackupXmlParser.Parse).ToList();
        return BackupUnitFilter.Filter(units, department, county, gender, category);
    }

    /// <summary>
    /// Generates a preview of applying the given EUP uniform to the given backup unit.
    /// Enforces freemode ped compatibility and gender match. Never writes to disk.
    /// </summary>
    public static BackupUniformPatchPreview PreviewAssignment(
        EupUniformDefinition uniform,
        BackupUnitDefinition unit,
        string xmlFilePath)
    {
        var mismatchWarnings = new List<string>();
        var warnings = new List<string>();
        bool canApply = true;
        bool isReadOnly = false;

        // 1. Freemode ped check — EUP components only work on freemode peds
        if (uniform.Components.Count > 0 || uniform.Props.Count > 0)
        {
            if (!EupInferenceHelper.IsFreemodePed(unit.PedModel))
            {
                mismatchWarnings.Add(
                    $"Target ped '{unit.PedModel ?? "unknown"}' may not support EUP component uniforms. " +
                    "Use mp_m_freemode_01 or mp_f_freemode_01 for EUP outfits.");
                canApply = false;
                isReadOnly = true;
            }
        }

        // 2. Gender × freemode ped check
        if (EupInferenceHelper.IsFreemodePed(unit.PedModel))
        {
            var targetGender = EupInferenceHelper.InferGenderFromPedModel(unit.PedModel);

            if (uniform.Gender == EupGender.Male && targetGender == EupGender.Female)
            {
                mismatchWarnings.Add(
                    "Selected uniform is Male but target backup ped is mp_f_freemode_01 (Female).");
                canApply = false;
            }
            else if (uniform.Gender == EupGender.Female && targetGender == EupGender.Male)
            {
                mismatchWarnings.Add(
                    "Selected uniform is Female but target backup ped is mp_m_freemode_01 (Male).");
                canApply = false;
            }
            else if (uniform.Gender == EupGender.Unknown)
            {
                warnings.Add("Uniform gender could not be determined. Review before applying.");
                // CanApply stays true if ped is freemode and XML structure supported
            }
        }

        // 3. Department mismatch (advisory warning, does not block)
        if (uniform.Department != "Unknown" && unit.Agency.Length > 0 &&
            !unit.Agency.Equals(uniform.Department, StringComparison.OrdinalIgnoreCase))
        {
            mismatchWarnings.Add(
                $"Selected uniform appears {uniform.Department} but target unit is {unit.Agency}.");
        }

        // 4. County/region mismatch (advisory warning, does not block)
        if (uniform.County != "Unknown" && uniform.County != "Statewide" &&
            unit.Region.Length > 0 &&
            !unit.Region.Equals(uniform.County, StringComparison.OrdinalIgnoreCase))
        {
            mismatchWarnings.Add(
                $"Uniform county is {uniform.County} but backup unit region is {unit.Region}.");
        }

        // 5. EUP format supportability
        bool formatSupported = uniform.Metadata.TryGetValue("Supported", out var sup) && sup == "true";
        if (!formatSupported && uniform.Components.Count > 0)
        {
            warnings.Add("Unknown EUP format — preview only. Apply disabled until format is confirmed.");
            canApply = false;
            isReadOnly = true;
        }

        // 6. Build before/after preview lines from the XML file
        string[] beforeLines = [];
        string[] afterLines = [];
        try
        {
            var rawLines = File.ReadAllLines(xmlFilePath);
            beforeLines = rawLines
                .Where(l => l.Contains(unit.Agency, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var uniformNameToApply = uniform.DisplayName;
            afterLines = beforeLines.Select(l =>
            {
                if (l.Contains("UniformName", StringComparison.OrdinalIgnoreCase)) return l;
                if (l.TrimEnd().EndsWith("/>"))
                    return l.TrimEnd()[..^2] + $" UniformName=\"{uniformNameToApply}\" />";
                return l + $" <!-- UniformName=\"{uniformNameToApply}\" -->";
            }).ToArray();

            if (beforeLines.Length == 0)
            {
                warnings.Add($"No XML entries found matching agency '{unit.Agency}'. Cannot apply.");
                canApply = false;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not read XML file: {ex.Message}");
            canApply = false;
        }

        float confidence = mismatchWarnings.Count == 0 ? uniform.Confidence
                         : uniform.Confidence * 0.5f;

        return new BackupUniformPatchPreview
        {
            SourceFile = xmlFilePath,
            MappingName = uniform.DisplayName,
            BeforeLines = beforeLines,
            AfterLines = afterLines,
            Warnings = [.. warnings],
            MismatchWarnings = [.. mismatchWarnings],
            CanApply = canApply && !isReadOnly,
            IsReadOnlyPreview = isReadOnly,
            Confidence = confidence,
            TargetAgency = unit.Agency,
            TargetUnitType = unit.UnitType,
            TargetPedModel = unit.PedModel,
            UniformNameToApply = uniform.DisplayName,
        };
    }

    /// <summary>
    /// Applies the preview to disk using XDocument-based stable node patching.
    /// Creates a timestamped backup before writing. Never writes if CanApply is false.
    /// </summary>
    public static (bool Applied, string? BackupPath, string? Error) ApplyAssignment(
        BackupUniformPatchPreview preview)
    {
        if (!preview.CanApply)
            return (false, null, "Preview CanApply is false. Review warnings before applying.");

        if (string.IsNullOrEmpty(preview.SourceFile) || !File.Exists(preview.SourceFile))
            return (false, null, "Source file not found.");

        if (string.IsNullOrEmpty(preview.UniformNameToApply))
            return (false, null, "No uniform name specified in preview.");

        // Timestamped backup — never overwrite a previous backup
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var bakPath = BuildUniqueBackupPath(preview.SourceFile, timestamp);
        try
        {
            File.Copy(preview.SourceFile, bakPath, overwrite: false);
            Core.AppLogger.Info($"[BACKUP_EASY_EDITOR_BAK] {Path.GetFileName(bakPath)}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Could not create backup: {ex.Message}");
        }

        var (changed, error) = BackupXmlParser.ApplyPatch(
            preview.SourceFile,
            preview.TargetAgency ?? "",
            preview.TargetUnitType,
            preview.TargetPedModel,
            preview.UniformNameToApply);

        if (error is not null || changed == 0)
        {
            // Delete the backup we just created since nothing was written
            try { File.Delete(bakPath); } catch { }
            return (false, null, error ?? "No changes applied — uniform may already be set.");
        }

        return (true, bakPath, null);
    }

    private static string BuildUniqueBackupPath(string sourceFile, string timestamp)
    {
        var basePath = sourceFile + $".bak.{timestamp}";
        if (!File.Exists(basePath))
            return basePath;

        var attempt = 1;
        while (true)
        {
            var candidate = $"{basePath}_{attempt}";
            if (!File.Exists(candidate))
                return candidate;
            attempt++;
        }
    }

    private static bool IsAny(string? value) =>
        string.IsNullOrEmpty(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase);

    private bool EupAppears()
    {
        try
        {
            if (File.Exists(Path.Combine(_gtaPath, "plugins", "lspdfr", "EUPSettings.xml"))) return true;
            if (Directory.Exists(Path.Combine(_gtaPath, "plugins", "lspdfr", "EUP Menu"))) return true;
            if (Directory.Exists(Path.Combine(_gtaPath, "Eup"))) return true;
            var lspdfrPlugins = Path.Combine(_gtaPath, "plugins", "lspdfr");
            if (Directory.Exists(lspdfrPlugins) &&
                Directory.EnumerateFiles(lspdfrPlugins, "*eup*.xml", SearchOption.AllDirectories).Any())
                return true;
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"BackupEasyEditorService.EupAppears error: {ex.Message}");
        }
        return false;
    }

    private string RelativePath(string absolute) =>
        string.IsNullOrEmpty(_gtaPath) ? absolute : Path.GetRelativePath(_gtaPath, absolute);
}
