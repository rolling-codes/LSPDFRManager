namespace LSPDFRManager.Domain;

/// <summary>One shared DLL found in more than one location under the GTA V folder.</summary>
/// <param name="DllName">File name without path (e.g. "RAGENativeUI.dll").</param>
/// <param name="Copies">All discovered file paths for this DLL.</param>
/// <param name="IsKnownSharedDep">True if the DLL is on the well-known shared-dependency list.</param>
public sealed record DllDuplicateResult(
    string DllName,
    IReadOnlyList<string> Copies,
    bool IsKnownSharedDep)
{
    public int Count => Copies.Count;
}
