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
