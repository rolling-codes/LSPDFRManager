using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Probes the GTA V install folder to confirm whether detected dependencies are
/// actually present, missing, or cannot be verified.
/// </summary>
public sealed class DependencyProbeService : IDependencyProbeService
{
    public DependencyProbeResult Probe(string gtaPath, DependencyDetectionResult dependencies)
    {
        if (!dependencies.HasWarnings)
            return DependencyProbeResult.Empty;

        var validPath = !string.IsNullOrWhiteSpace(gtaPath) && Directory.Exists(gtaPath);
        var probes = new List<DependencyProbe>();

        foreach (var warning in dependencies.Warnings)
        {
            var probe = BuildProbe(warning.Name, gtaPath, validPath);
            probes.Add(probe);
        }

        return new DependencyProbeResult { Probes = probes };
    }

    private static DependencyProbe BuildProbe(string name, string gtaPath, bool validPath)
    {
        // OpenIV — always NotApplicable
        if (name.Contains("OpenIV", StringComparison.OrdinalIgnoreCase))
        {
            return new DependencyProbe
            {
                Name = name,
                Status = DependencyProbeStatus.NotApplicable,
                Message = "OIV packages require manual installation via OpenIV or a compatible installer.",
            };
        }

        // Unknown path — can't probe
        if (!validPath)
        {
            return new DependencyProbe
            {
                Name = name,
                Status = DependencyProbeStatus.Unknown,
                Message = "GTA V path is not configured or does not exist — cannot verify.",
            };
        }

        if (name.Contains("ScriptHookVDotNet", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("SHVDN", StringComparison.OrdinalIgnoreCase))
        {
            return ProbeFiles(name, gtaPath,
                ["ScriptHookVDotNet.asi", "ScriptHookVDotNet2.dll", "ScriptHookVDotNet3.dll"],
                anyRequired: true);
        }

        if (name.Contains("Script Hook V", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Script Hook", StringComparison.OrdinalIgnoreCase) &&
             !name.Contains("DotNet", StringComparison.OrdinalIgnoreCase)))
        {
            return ProbeFiles(name, gtaPath,
                ["ScriptHookV.dll", "dinput8.dll"],
                anyRequired: true);
        }

        if (name.Contains("ASI Loader", StringComparison.OrdinalIgnoreCase))
        {
            return ProbeFiles(name, gtaPath,
                ["dinput8.dll"],
                anyRequired: true);
        }

        if (name.Contains("LSPDFR", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("RAGE", StringComparison.OrdinalIgnoreCase))
        {
            // Present if plugins/LSPDFR.dll OR directory plugins/lspdfr exists
            var dllPath = Path.Combine(gtaPath, "plugins", "LSPDFR.dll");
            var dirPath = Path.Combine(gtaPath, "plugins", "lspdfr");
            var evidence = new List<string>();
            if (File.Exists(dllPath)) evidence.Add("plugins/LSPDFR.dll");
            if (Directory.Exists(dirPath)) evidence.Add("plugins/lspdfr/");

            if (evidence.Count > 0)
            {
                return new DependencyProbe
                {
                    Name = name,
                    Status = DependencyProbeStatus.Present,
                    Evidence = evidence,
                    Message = $"Installed ({string.Join(", ", evidence)})",
                };
            }

            return new DependencyProbe
            {
                Name = name,
                Status = DependencyProbeStatus.Missing,
                Evidence = ["plugins/LSPDFR.dll", "plugins/lspdfr/"],
                Message = "Not found — plugins/LSPDFR.dll and plugins/lspdfr/ are absent.",
            };
        }

        if (name.Contains("RAGE Plugin Hook", StringComparison.OrdinalIgnoreCase))
        {
            return ProbeFiles(name, gtaPath,
                ["RAGEPluginHook.exe"],
                anyRequired: true);
        }

        // Unknown dependency type
        return new DependencyProbe
        {
            Name = name,
            Status = DependencyProbeStatus.Unknown,
            Message = "No probe rule defined for this dependency.",
        };
    }

    private static DependencyProbe ProbeFiles(string name, string gtaPath, string[] relPaths, bool anyRequired)
    {
        var found = new List<string>();
        var missing = new List<string>();

        foreach (var rel in relPaths)
        {
            var full = Path.Combine(gtaPath, rel);
            if (File.Exists(full))
                found.Add(rel);
            else
                missing.Add(rel);
        }

        bool present = anyRequired ? found.Count > 0 : missing.Count == 0;

        if (present)
        {
            return new DependencyProbe
            {
                Name = name,
                Status = DependencyProbeStatus.Present,
                Evidence = found,
                Message = $"Installed ({string.Join(", ", found)})",
            };
        }

        return new DependencyProbe
        {
            Name = name,
            Status = DependencyProbeStatus.Missing,
            Evidence = relPaths,
            Message = $"Not found — checked: {string.Join(", ", relPaths)}",
        };
    }
}
