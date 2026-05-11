using System.Xml.Linq;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Reads and updates the <c>dlclist.xml</c> that tells GTA V which vehicle
/// DLC packs to load on startup.
/// <para>
/// The file must be extracted from <c>update.rpf</c> via OpenIV and placed at:<br/>
/// <c>{GtaRoot}/mods/update/update.rpf/common/data/dlclist.xml</c>
/// </para>
/// </summary>
public static class DlcListService
{
    private static string XmlPath =>
        Path.Combine(AppConfig.Instance.GtaPath,
            "mods", "update", "update.rpf", "common", "data", "dlclist.xml");

    /// <summary><c>true</c> if the mods-folder dlclist.xml exists.</summary>
    public static bool IsAvailable => File.Exists(XmlPath);

    /// <summary>
    /// Adds <paramref name="dlcPackName"/> to the DLC list if not already present.
    /// Logs a warning when the file is missing rather than throwing.
    /// </summary>
    public static void AddEntry(string dlcPackName)
    {
        var normalizedPackName = NormalizePackName(dlcPackName);
        if (string.IsNullOrWhiteSpace(normalizedPackName)) return;

        var path = XmlPath;
        if (!File.Exists(path))
        {
            AppLogger.Warning(
                $"dlclist.xml not found — cannot register '{normalizedPackName}'. " +
                "Use OpenIV to extract it to mods/update/update.rpf/common/data/");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var paths = doc.Root?.Element("Paths");
            if (paths is null) return;

            var canonicalEntry = ToCanonicalEntry(normalizedPackName);
            var exists = paths.Elements("Item")
                .Any(e => EntryMatchesPack(e.Value, normalizedPackName));

            if (!exists)
            {
                paths.Add(new XElement("Item", canonicalEntry));
                doc.Save(path);
                AppLogger.Info($"dlclist.xml: added {canonicalEntry}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"dlclist.xml update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes <paramref name="dlcPackName"/> from the DLC list on uninstall.
    /// </summary>
    public static void RemoveEntry(string dlcPackName)
    {
        var normalizedPackName = NormalizePackName(dlcPackName);
        if (string.IsNullOrWhiteSpace(normalizedPackName)) return;

        var path = XmlPath;
        if (!File.Exists(path)) return;

        try
        {
            var doc = XDocument.Load(path);
            var paths = doc.Root?.Element("Paths");
            if (paths is null) return;

            var matchingItems = paths.Elements("Item")
                .Where(e => EntryMatchesPack(e.Value, normalizedPackName))
                .ToList();

            foreach (var item in matchingItems)
                item.Remove();

            if (matchingItems.Count > 0)
            {
                doc.Save(path);
                AppLogger.Info($"dlclist.xml: removed {ToCanonicalEntry(normalizedPackName)}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"dlclist.xml update failed: {ex.Message}");
        }
    }

    private static string ToCanonicalEntry(string dlcPackName) => $"dlcpacks:/{dlcPackName}/";

    private static bool EntryMatchesPack(string entryValue, string dlcPackName)
    {
        return NormalizePackName(entryValue).Equals(dlcPackName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePackName(string value)
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
