using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public static class GtaDriftDetector
{
    public static IReadOnlyList<string> Detect(VersionBundle current, GtaBaseline? baseline)
    {
        if (baseline is null)
            return [];

        bool currentHasData = current.GtaVersion is not null
            || current.GtaExeFileSizeBytes is not null
            || current.GtaExeLastWriteTimeUtc is not null;

        bool baselineHasData = baseline.GtaVersion is not null
            || baseline.GtaExeFileSizeBytes is not null
            || baseline.GtaExeLastWriteTimeUtc is not null;

        if (!currentHasData || !baselineHasData)
            return ["GTA5.exe baseline comparison unavailable — not enough data to detect changes."];

        // Hash is definitive when available on both sides
        if (current.GtaHash is not null && baseline.GtaHash is not null)
        {
            return string.Equals(current.GtaHash, baseline.GtaHash, StringComparison.OrdinalIgnoreCase)
                ? []
                : ["GTA5.exe version changed since the last successful readiness check."];
        }

        // Version change takes priority over size/time
        if (current.GtaVersion is not null && baseline.GtaVersion is not null &&
            !string.Equals(current.GtaVersion, baseline.GtaVersion, StringComparison.OrdinalIgnoreCase))
        {
            return ["GTA5.exe version changed since the last successful readiness check."];
        }

        bool sizeChanged = current.GtaExeFileSizeBytes.HasValue
            && baseline.GtaExeFileSizeBytes.HasValue
            && current.GtaExeFileSizeBytes != baseline.GtaExeFileSizeBytes;

        bool timeChanged = current.GtaExeLastWriteTimeUtc.HasValue
            && baseline.GtaExeLastWriteTimeUtc.HasValue
            && current.GtaExeLastWriteTimeUtc != baseline.GtaExeLastWriteTimeUtc;

        return sizeChanged || timeChanged
            ? ["GTA5.exe appears to have changed since the last successful readiness check."]
            : [];
    }
}
