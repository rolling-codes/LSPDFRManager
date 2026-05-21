using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface ICleanupMode
{
    CleanupMode Mode { get; }
    CleanupModePreset Apply(CleanupScanResult scanResult);
}
