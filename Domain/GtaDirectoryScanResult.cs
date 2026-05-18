namespace LSPDFRManager.Domain;

public record GtaDirectoryScanResult(
    string Path,
    bool IsValidGtaRoot,
    bool HasLspdfrCore,
    bool HasRagePluginHook,
    bool HasRagePluginHookDll,
    string? GtaExeFound,
    string? LspdfrCoreFound,
    string? RagePluginHookFound
)
{
    public bool IsReadyForLspdfr => IsValidGtaRoot && HasLspdfrCore && HasRagePluginHook && HasRagePluginHookDll;
}
