using System.Xml.Linq;
using LSPDFRManager.Core;
using LSPDFRManager.Models;

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
        if (string.IsNullOrWhiteSpace(dlcPackName)) return;

        var path = XmlPath;
        if (!File.Exists(path))
        {
            AppLogger.Warning(
                $"dlclist.xml not found — cannot register '{dlcPackName}'. " +
                "Use OpenIV to extract it to mods/update/update.rpf/common/data/");
            return;
        }

        try
        {
            var doc    = XDocument.Load(path);
            var paths  = doc.Root?.Element("Paths");
            if (paths is null) return;

            var entry  = $@"dlcpacks:\{dlcPackName}\";
            var exists = paths.Elements("Item")
                .Any(e => e.Value.Trim().Equals(entry, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                paths.Add(new XElement("Item", entry));
                doc.Save(path);
                AppLogger.Info($"dlclist.xml: added {entry}");
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
        if (string.IsNullOrWhiteSpace(dlcPackName)) return;

        var path = XmlPath;
        if (!File.Exists(path)) return;

        try
        {
            var doc   = XDocument.Load(path);
            var paths = doc.Root?.Element("Paths");
            if (paths is null) return;

            var entry = $@"dlcpacks:\{dlcPackName}\";
            var item  = paths.Elements("Item")
                .FirstOrDefault(e => e.Value.Trim()
                    .Equals(entry, StringComparison.OrdinalIgnoreCase));

            if (item is not null)
            {
                item.Remove();
                doc.Save(path);
                AppLogger.Info($"dlclist.xml: removed {entry}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"dlclist.xml update failed: {ex.Message}");
        }
    }
}
