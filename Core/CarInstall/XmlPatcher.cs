using System.Xml.Linq;
using System.Xml.XPath;
using LSPDFRManager.Core;
using LSPDFRManager.OpenIv.CarInstall.Models;

namespace LSPDFRManager.OpenIv.CarInstall;

/// <summary>
/// Applies XML patches to dlclist.xml files.
/// Loads, modifies, and saves XML deterministically.
/// </summary>
public class XmlPatcher : IXmlPatcher
{
    public void Apply(XmlPatch patch)
    {
        if (!File.Exists(patch.FilePath))
            throw new InvalidOperationException(
                $"XML patch file not found: {patch.FilePath}");

        try
        {
            var doc = XDocument.Load(patch.FilePath);
            var targetElement = doc.Root?.XPathSelectElement(patch.XPath);

            if (targetElement is null)
                throw new InvalidOperationException(
                    $"XPath '{patch.XPath}' not found in {patch.FilePath}");

            if (!targetElement.Elements("Item").Any(item => DlcListEntriesEqual(item.Value, patch.Value)))
            {
                targetElement.Add(new XElement("Item", ToCanonicalDlcEntry(patch.Value)));
                doc.Save(patch.FilePath);
                AppLogger.Info($"XML patch applied: {patch.FilePath} at {patch.XPath}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to apply XML patch to {patch.FilePath}: {ex.Message}", ex);
        }
    }

    private static bool DlcListEntriesEqual(string left, string right) =>
        NormalizeDlcEntry(left).Equals(NormalizeDlcEntry(right), StringComparison.OrdinalIgnoreCase);

    private static string ToCanonicalDlcEntry(string value)
    {
        var packName = NormalizeDlcEntry(value);
        return string.IsNullOrWhiteSpace(packName) ? value.Trim() : $"dlcpacks:/{packName}/";
    }

    private static string NormalizeDlcEntry(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim()
            .Replace('\\', '/')
            .Trim('/');

        const string prefix = "dlcpacks:";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized[prefix.Length..].Trim('/');

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }
}
