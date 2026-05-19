namespace LSPDFRManager.OpenIv.CarInstall.Models;

public class XmlPatch
{
    public required string FilePath { get; init; }
    public required string XPath { get; init; }
    public required string Value { get; init; }
}
