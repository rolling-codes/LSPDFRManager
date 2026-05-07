using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class DisabledModsScanner
{
    private static readonly string[] ScanFolders = ["plugins", "plugins/lspdfr", "scripts", "mods", "ELS"];

    public List<DisabledModEntry> Scan()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var results = new List<DisabledModEntry>();

        foreach (var sub in ScanFolders)
        {
            var dir = Path.Combine(gtaPath, sub);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.disabled", SearchOption.AllDirectories))
            {
                var originalName = Path.GetFileName(file)[..^".disabled".Length];
                var category = sub.Contains("lspdfr") ? "LSPDFR Plugin"
                    : sub == "scripts" ? "Script"
                    : sub == "mods" ? "Mod"
                    : sub == "ELS" ? "ELS"
                    : "Plugin";

                results.Add(new DisabledModEntry
                {
                    DisabledPath = file,
                    OriginalName = originalName,
                    Category = category,
                    ContainingFolder = Path.GetDirectoryName(file) ?? dir,
                    LikelyModName = Path.GetFileNameWithoutExtension(originalName),
                });
            }
        }

        return results;
    }

    public void Enable(DisabledModEntry entry)
    {
        var target = entry.DisabledPath[..^".disabled".Length];
        if (File.Exists(entry.DisabledPath))
            File.Move(entry.DisabledPath, target);
    }
}
