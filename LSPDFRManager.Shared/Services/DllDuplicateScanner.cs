using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Scans the GTA V folder for shared DLLs that appear in more than one location.
/// Does NOT delete or move any files.  Results feed the diagnostics UI and support bundle.
/// </summary>
public sealed class DllDuplicateScanner
{
    /// <summary>
    /// Well-known shared dependencies that plugins commonly bundle privately,
    /// causing version conflicts when duplicated.
    /// </summary>
    private static readonly HashSet<string> KnownSharedDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        "RAGENativeUI.dll",
        "LemonUI.RAGE.dll",
        "LemonUI.WinForms.dll",
        "Newtonsoft.Json.dll",
        "NAudio.dll",
        "NAudio.Core.dll",
        "NAudio.WinMM.dll",
        "NAudio.WinForms.dll",
        "ScriptHookVDotNet3.dll",
        "ScriptHookVDotNet2.dll",
        "NativeUI.dll",
        "Rage.dll",
    };

    /// <summary>
    /// Subdirectories under the GTA V root to scan. TopDirectoryOnly for each.
    /// </summary>
    private static readonly string[] ScanDirs =
    [
        "",           // GTA root
        "plugins",
        "plugins/LSPDFR",
        "scripts",
    ];

    /// <summary>
    /// Scans the GTA V folder and returns DLLs found in more than one location.
    /// </summary>
    public IReadOnlyList<DllDuplicateResult> Scan()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
            return [];

        // name (lowercase) → list of full paths
        var found = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var subdir in ScanDirs)
        {
            var dir = string.IsNullOrEmpty(subdir)
                ? gtaPath
                : Path.Combine(gtaPath, subdir.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (!found.TryGetValue(name, out var list))
                {
                    list = [];
                    found[name] = list;
                }
                list.Add(file);
            }
        }

        var results = new List<DllDuplicateResult>();
        foreach (var (name, paths) in found)
        {
            if (paths.Count < 2) continue;
            results.Add(new DllDuplicateResult(
                DllName: name,
                Copies: paths.AsReadOnly(),
                IsKnownSharedDep: KnownSharedDeps.Contains(name)));
        }

        // Known shared deps first, then alphabetical
        results.Sort((a, b) =>
        {
            var byKnown = b.IsKnownSharedDep.CompareTo(a.IsKnownSharedDep);
            return byKnown != 0 ? byKnown : string.Compare(a.DllName, b.DllName, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }
}
