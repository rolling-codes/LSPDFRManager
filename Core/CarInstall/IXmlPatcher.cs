using LSPDFRManager.OpenIv.CarInstall.Models;

namespace LSPDFRManager.OpenIv.CarInstall;

/// <summary>
/// Applies XML patch operations to files.
/// Deterministic: takes FilePath, XPath, Value and applies unchanged.
/// </summary>
public interface IXmlPatcher
{
    /// <summary>
    /// Applies XML patch: finds element at XPath in FilePath and adds a child Item element with Value.
    /// </summary>
    /// <exception cref="InvalidOperationException">If file not found or XPath invalid.</exception>
    void Apply(XmlPatch patch);
}
