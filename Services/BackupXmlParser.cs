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

    private static string? Attr(XElement e, string name) =>
        e.Attribute(name)?.Value is { Length: > 0 } v ? v : null;

    private static string? ChildText(XElement e, string name) =>
        e.Element(name)?.Value is { Length: > 0 } v ? v : null;
}
