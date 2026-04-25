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

            targetElement.Add(new XElement("Item", patch.Value));
            doc.Save(patch.FilePath);

            AppLogger.Info($"XML patch applied: {patch.FilePath} at {patch.XPath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to apply XML patch to {patch.FilePath}: {ex.Message}", ex);
        }
    }
}
