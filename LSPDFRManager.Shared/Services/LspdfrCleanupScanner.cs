using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class LspdfrCleanupScanner
{
    private static readonly HashSet<string> GtaExecutables =
        new(LspdfrInstallLocator.GtaExeCandidates, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RphCoreFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "RAGEPluginHook.exe",
        "RagePluginHook.dll",
    };

    private static readonly HashSet<string> SharedDependencyFileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Newtonsoft.Json.dll",
            "LemonUI.SHVDN3.dll",
            "RageNativeUI.dll",
            "NativeUI.dll",
            "ScriptHookVDotNet.asi",
            "ScriptHookV.dll",
            "Albo1125.Common.dll",
        };

    private static readonly HashSet<string> OptionalInfraFileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "dinput8.dll",
            "OpenIV.asi",
            "HeapAdjuster.asi",
            "PackfileLimitAdjuster.asi",
            "GTAVLauncherBypass.exe",
            "NAudio.dll",
        };

    private static readonly HashSet<string> LspdfrCoreRelPaths;

    static LspdfrCleanupScanner()
    {
        LspdfrCoreRelPaths = new HashSet<string>(
            LspdfrInstallLocator.LspdfrCoreCandidates
                .Select(c => c.Replace('\\', '/').ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    public static CleanupScanResult Scan(string gtaRoot)
    {
        var root = Path.GetFullPath(gtaRoot).TrimEnd(Path.DirectorySeparatorChar);
        var groups = new List<RemovalGroup>();

        var core = ScanLspdfrCore(root);
        if (core is not null) groups.Add(core);

        var rph = ScanRphCore(root);
        if (rph is not null) groups.Add(rph);

        var data = ScanLspdfrData(root);
        if (data is not null) groups.Add(data);

        groups.AddRange(ScanThirdPartyPlugins(root));

        var shared = ScanSharedDependencies(root);
        if (shared is not null) groups.Add(shared);

        var optional = ScanOptionalInfrastructure(root);
        if (optional is not null) groups.Add(optional);

        return new CleanupScanResult
        {
            GtaRoot = root,
            Groups = groups,
            ScannedAt = DateTimeOffset.UtcNow,
        };
    }

    public static bool IsGtaExecutable(string path) =>
        GtaExecutables.Contains(Path.GetFileName(path));

    public static bool IsOutsideRoot(string gtaRoot, string fullPath)
    {
        var root = Path.GetFullPath(gtaRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(fullPath);
        return !candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    // ── Private scan methods ───────────────────────────────────────────────

    private static RemovalGroup? ScanLspdfrCore(string gtaRoot)
    {
        var candidates = new List<RemovalCandidate>();

        foreach (var rel in LspdfrInstallLocator.LspdfrCoreCandidates)
        {
            var norm = rel.Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.Combine(gtaRoot, norm);
            if (!File.Exists(full)) continue;

            candidates.Add(new RemovalCandidate
            {
                RelativePath = norm,
                FullPath = full,
                Classification = CandidateClassification.LspdfrCore,
                RiskLevel = CleanupRiskLevel.Low,
                Reason = "Safe to remove: known reinstallable LSPDFR core file",
            });
        }

        if (candidates.Count == 0) return null;
        return new RemovalGroup
        {
            Label = "LSPDFR Core",
            GroupKind = CandidateClassification.LspdfrCore,
            Candidates = candidates,
        };
    }

    private static RemovalGroup? ScanRphCore(string gtaRoot)
    {
        var candidates = new List<RemovalCandidate>();

        foreach (var fileName in RphCoreFiles)
        {
            var full = Path.Combine(gtaRoot, fileName);
            if (!File.Exists(full)) continue;

            candidates.Add(new RemovalCandidate
            {
                RelativePath = fileName,
                FullPath = full,
                Classification = CandidateClassification.RphCore,
                RiskLevel = CleanupRiskLevel.Low,
                Reason = "Safe to remove: known reinstallable RAGE Plugin Hook file",
            });
        }

        if (candidates.Count == 0) return null;
        return new RemovalGroup
        {
            Label = "RAGE Plugin Hook",
            GroupKind = CandidateClassification.RphCore,
            Candidates = candidates,
        };
    }

    private static RemovalGroup? ScanLspdfrData(string gtaRoot)
    {
        foreach (var rel in LspdfrInstallLocator.LspdfrFolderCandidates)
        {
            var norm = rel.Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.Combine(gtaRoot, norm);
            if (!Directory.Exists(full)) continue;

            return new RemovalGroup
            {
                Label = "LSPDFR Data",
                GroupKind = CandidateClassification.LspdfrData,
                Candidates =
                [
                    new RemovalCandidate
                    {
                        RelativePath = norm,
                        FullPath = full,
                        Classification = CandidateClassification.LspdfrData,
                        RiskLevel = CleanupRiskLevel.Medium,
                        Reason = "User data risk: LSPDFR data/config folder",
                        IsDirectory = true,
                    },
                ],
            };
        }

        return null;
    }

    private static IEnumerable<RemovalGroup> ScanThirdPartyPlugins(string gtaRoot)
    {
        var pluginDir = Path.Combine(gtaRoot, "plugins", "lspdfr");
        if (!Directory.Exists(pluginDir)) yield break;

        // Collect all files, skip LSPDFR core and shared deps
        var allFiles = Directory.GetFiles(pluginDir, "*", SearchOption.AllDirectories);
        var byBaseName = new Dictionary<string, List<(string Path, bool IsDir)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in allFiles)
        {
            var relToGta = Path.GetRelativePath(gtaRoot, file).Replace('\\', '/');

            if (LspdfrCoreRelPaths.Contains(relToGta.ToLowerInvariant())) continue;
            if (SharedDependencyFileNames.Contains(Path.GetFileName(file))) continue;

            var baseName = Path.GetFileNameWithoutExtension(file);
            if (!byBaseName.TryGetValue(baseName, out var list)) { list = []; byBaseName[baseName] = list; }
            list.Add((file, false));
        }

        // Subdirectories of plugins/lspdfr/ → plugin data folder
        foreach (var dir in Directory.GetDirectories(pluginDir))
        {
            var dirName = Path.GetFileName(dir);
            if (!byBaseName.TryGetValue(dirName, out var list)) { list = []; byBaseName[dirName] = list; }
            list.Insert(0, (dir, true));
        }

        foreach (var (baseName, items) in byBaseName)
        {
            var candidates = new List<RemovalCandidate>();

            foreach (var (itemPath, isDir) in items)
            {
                if (isDir)
                {
                    candidates.Add(new RemovalCandidate
                    {
                        RelativePath = Path.GetRelativePath(gtaRoot, itemPath).Replace('\\', '/'),
                        FullPath = itemPath,
                        Classification = CandidateClassification.PluginDataFolder,
                        RiskLevel = CleanupRiskLevel.Medium,
                        Reason = "Plugin risk: plugin data folder",
                        IsDirectory = true,
                    });
                }
                else
                {
                    var ext = Path.GetExtension(itemPath).ToLowerInvariant();
                    var isBinary = ext is ".dll" or ".asi";
                    candidates.Add(new RemovalCandidate
                    {
                        RelativePath = Path.GetRelativePath(gtaRoot, itemPath).Replace('\\', '/'),
                        FullPath = itemPath,
                        Classification = isBinary
                            ? CandidateClassification.ThirdPartyPlugin
                            : CandidateClassification.PluginConfig,
                        RiskLevel = CleanupRiskLevel.Medium,
                        Reason = isBinary
                            ? "Plugin risk: third-party LSPDFR plugin"
                            : "Plugin risk: plugin configuration file",
                    });
                }
            }

            // Only emit group if it contains at least one plugin binary
            if (!candidates.Any(c => c.Classification == CandidateClassification.ThirdPartyPlugin))
                continue;

            yield return new RemovalGroup
            {
                Label = baseName,
                GroupKind = CandidateClassification.ThirdPartyPlugin,
                Candidates = candidates,
            };
        }
    }

    private static RemovalGroup? ScanSharedDependencies(string gtaRoot)
    {
        var candidates = new List<RemovalCandidate>();
        var searchDirs = new[]
        {
            gtaRoot,
            Path.Combine(gtaRoot, "plugins"),
            Path.Combine(gtaRoot, "plugins", "lspdfr"),
        };

        foreach (var depName in SharedDependencyFileNames)
        {
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                var full = Path.Combine(dir, depName);
                if (!File.Exists(full)) continue;

                candidates.Add(new RemovalCandidate
                {
                    RelativePath = Path.GetRelativePath(gtaRoot, full).Replace('\\', '/'),
                    FullPath = full,
                    Classification = CandidateClassification.SharedDependency,
                    RiskLevel = CleanupRiskLevel.High,
                    Reason = "Shared dependency risk: may be used by multiple plugins",
                });
                break;
            }
        }

        if (candidates.Count == 0) return null;
        return new RemovalGroup
        {
            Label = "Shared Dependencies",
            GroupKind = CandidateClassification.SharedDependency,
            Candidates = candidates,
        };
    }

    private static RemovalGroup? ScanOptionalInfrastructure(string gtaRoot)
    {
        var candidates = new List<RemovalCandidate>();

        foreach (var fileName in OptionalInfraFileNames)
        {
            var full = Path.Combine(gtaRoot, fileName);
            if (!File.Exists(full)) continue;

            candidates.Add(new RemovalCandidate
            {
                RelativePath = fileName,
                FullPath = full,
                Classification = CandidateClassification.OptionalInfrastructure,
                RiskLevel = CleanupRiskLevel.Medium,
                Reason = "Optional infrastructure: commonly reinstallable mod support file",
            });
        }

        if (candidates.Count == 0) return null;
        return new RemovalGroup
        {
            Label = "Optional Infrastructure",
            GroupKind = CandidateClassification.OptionalInfrastructure,
            Candidates = candidates,
        };
    }
}
