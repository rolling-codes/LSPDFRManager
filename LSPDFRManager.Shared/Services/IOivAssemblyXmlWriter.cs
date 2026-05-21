using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface IOivAssemblyXmlWriter
{
    /// <summary>
    /// Produces the <c>assembly.xml</c> content string for <paramref name="plan"/>.
    /// The plan must be valid before calling this method.
    /// </summary>
    string Write(OivPackagePlan plan);
}
