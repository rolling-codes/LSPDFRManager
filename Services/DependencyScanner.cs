using System.Diagnostics;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class DependencyScanner
{
    private record KnownDependency(string Name, string RelativePath, bool IsRequired);

    private static readonly KnownDependency[] KnownDependencies =
    [
        new("GTA5.exe",                      "GTA5.exe",                            true),
        new("PlayGTAV.exe",                  "PlayGTAV.exe",                        false),
        new("RAGEPluginHook.exe",            "RAGEPluginHook.exe",                  false),
        new("LSPDFR.dll",                    @"plugins\LSPDFR.dll",                 false),
        new("ScriptHookV.dll",               "ScriptHookV.dll",                     false),
        new("ScriptHookVDotNet.asi",         "ScriptHookVDotNet.asi",               false),
        new("dinput8.dll",                   "dinput8.dll",                         false),
        new("NativeUI.dll",                  @"plugins\NativeUI.dll",               false),
        new("RageNativeUI.dll",              @"plugins\RageNativeUI.dll",           false),
        new("OpenIV.asi",                    "OpenIV.asi",                          false),
        new("HeapAdjuster.asi",              "HeapAdjuster.asi",                    false),
        new("PackfileLimitAdjuster.asi",     "PackfileLimitAdjuster.asi",           false),
        new("LemonUI.dll",                   @"plugins\lspdfr\LemonUI.SHVDN3.dll",  false),
        new("Newtonsoft.Json.dll",           @"plugins\Newtonsoft.Json.dll",        false),
        new("NAudio.dll",                    @"plugins\NAudio.dll",                 false),
        new("ELS folder",                    "ELS",                                 false),
    ];

    private readonly HashSet<string> _ignored = [];

    public List<DependencyScanResult> Scan()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var results = new List<DependencyScanResult>();

        foreach (var dep in KnownDependencies)
        {
            var fullPath = Path.Combine(gtaPath, dep.RelativePath);
            var exists = File.Exists(fullPath) || Directory.Exists(fullPath);
            var disabledPath = fullPath + ".disabled";
            var isDisabled = !exists && File.Exists(disabledPath);

            string? version = null;
            if (exists && File.Exists(fullPath) && fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                try { version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion; } catch { }
            }

            var status = exists ? DependencyStatus.Installed
                : isDisabled ? DependencyStatus.Disabled
                : dep.IsRequired ? DependencyStatus.Missing
                : DependencyStatus.Optional;

            results.Add(new DependencyScanResult
            {
                Name = dep.Name,
                ExpectedPath = fullPath,
                ActualPath = exists ? fullPath : null,
                Status = status,
                Version = version,
                Note = dep.IsRequired && !exists ? "Required for GTA V to run." : null,
                IsIgnored = _ignored.Contains(dep.Name),
            });
        }

        return results;
    }

    public void IgnoreDependency(string name) => _ignored.Add(name);
    public void UnignoreDependency(string name) => _ignored.Remove(name);
}
