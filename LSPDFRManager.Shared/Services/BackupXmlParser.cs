using System.Xml.Linq;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public static class BackupXmlParser
{
    private static readonly HashSet<string> UnitElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Unit", "BackupUnit", "Officer", "Ped", "Vehicle", "Agency"
    };

    public static List<BackupUnitDefinition> Parse(string xmlFilePath)
    {
        try
        {
            var doc = XDocument.Load(xmlFilePath);
            var units = doc.Descendants()
                .Where(e => UnitElementNames.Contains(e.Name.LocalName) && IsUnitEntry(e))
                .ToList();

            if (units.Count == 0) return [];

            return units.Select(e => new BackupUnitDefinition
            {
                Agency = Attr(e, "Agency") ?? ChildText(e, "Agency") ?? "",
                Region = Attr(e, "Region") ?? ChildText(e, "Region") ?? "",
                UnitType = Attr(e, "UnitType") ?? ChildText(e, "UnitType") ?? "",
                PedModel = Attr(e, "PedModel") ?? ChildText(e, "PedModel"),
                VehicleModel = Attr(e, "VehicleModel") ?? ChildText(e, "VehicleModel"),
                UniformName = Attr(e, "UniformName") ?? ChildText(e, "UniformName"),
            }).ToList();
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            Core.AppLogger.Warning($"BackupXmlParser.Parse failed for '{xmlFilePath}': {ex.Message}");
            return [];
        }
    }

    public static DiagnosticFinding? Diagnose(string xmlFilePath)
    {
        try
        {
            var doc = XDocument.Load(xmlFilePath);
            var units = doc.Descendants()
                .Where(e => UnitElementNames.Contains(e.Name.LocalName) && IsUnitEntry(e))
                .ToList();

            if (units.Count == 0)
                return new DiagnosticFinding
                {
                    Category = "Config",
                    Title = "Backup XML references unsupported or unknown structure",
                    Detail = $"No recognizable backup unit elements found in '{xmlFilePath}'.",
                    Severity = DiagnosticSeverity.Warning,
                    AffectedPath = xmlFilePath,
                };

            return null;
        }
        catch (FileNotFoundException)
        {
            return ErrorFinding(xmlFilePath, "File not found.");
        }
        catch (System.Xml.XmlException ex)
        {
            return ErrorFinding(xmlFilePath, ex.Message);
        }
        catch (IOException ex)
        {
            return ErrorFinding(xmlFilePath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorFinding(xmlFilePath, ex.Message);
        }
    }

    private static DiagnosticFinding ErrorFinding(string path, string detail) => new()
    {
        Category = "Config",
        Title = "Backup XML could not be read",
        Detail = detail,
        Severity = DiagnosticSeverity.Error,
        AffectedPath = path,
    };

    private static readonly HashSet<string> ModelAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PedModel", "Model", "VehicleModel", "UniformName", "Ped", "Vehicle"
    };

    private static bool IsUnitEntry(XElement e)
    {
        foreach (var attr in e.Attributes())
            if (ModelAttributeNames.Contains(attr.Name.LocalName)) return true;
        foreach (var child in e.Elements())
            if (ModelAttributeNames.Contains(child.Name.LocalName)) return true;
        return false;
    }

    /// <summary>
    /// Applies a UniformName attribute to all unit elements matching the stable key
    /// (agency required; unitType and pedModel optional filters). Uses XDocument so
    /// patch targets are identified structurally, not by line content.
    /// Returns (changed count, error message). Never throws.
    /// </summary>
    public static (int Changed, string? Error) ApplyPatch(
        string xmlFilePath,
        string agency,
        string? unitType,
        string? pedModel,
        string uniformName)
    {
        if (string.IsNullOrWhiteSpace(uniformName))
            return (0, "UniformName must not be empty.");

        XDocument doc;
        try { doc = XDocument.Load(xmlFilePath); }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"BackupXmlParser.ApplyPatch load error '{xmlFilePath}': {ex.Message}");
            return (0, $"Could not load XML: {ex.Message}");
        }

        var candidates = doc.Descendants()
            .Where(e => UnitElementNames.Contains(e.Name.LocalName) && IsUnitEntry(e))
            .Where(e =>
            {
                var a = Attr(e, "Agency") ?? ChildText(e, "Agency") ?? "";
                if (!a.Equals(agency, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrEmpty(unitType))
                {
                    var ut = Attr(e, "UnitType") ?? ChildText(e, "UnitType") ?? "";
                    if (!ut.Equals(unitType, StringComparison.OrdinalIgnoreCase)) return false;
                }
                if (!string.IsNullOrEmpty(pedModel))
                {
                    var pm = Attr(e, "PedModel") ?? ChildText(e, "PedModel") ?? "";
                    if (!pm.Equals(pedModel, StringComparison.OrdinalIgnoreCase)) return false;
                }
                return true;
            })
            .ToList();

        if (candidates.Count == 0)
            return (0, $"No matching unit found for agency='{agency}'.");

        int changed = 0;
        foreach (var el in candidates)
        {
            var existing = el.Attribute("UniformName");
            if (existing is not null)
            {
                if (existing.Value.Equals(uniformName, StringComparison.OrdinalIgnoreCase))
                    continue; // already set — no change
                existing.SetValue(uniformName);
            }
            else
            {
                el.SetAttributeValue("UniformName", uniformName);
            }
            changed++;
        }

        if (changed == 0)
            return (0, "All matching units already have the specified uniform name.");

        try { doc.Save(xmlFilePath); }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"BackupXmlParser.ApplyPatch save error '{xmlFilePath}': {ex.Message}");
            return (0, $"Could not save XML: {ex.Message}");
        }

        Core.AppLogger.Info($"[BACKUP_XML_PATCH] agency={agency} uniform={uniformName} changed={changed} file={Path.GetFileName(xmlFilePath)}");
        return (changed, null);
    }

    private static string? Attr(XElement e, string name) =>
        e.Attribute(name)?.Value is { Length: > 0 } v ? v : null;

    private static string? ChildText(XElement e, string name) =>
        e.Element(name)?.Value is { Length: > 0 } v ? v : null;
}
