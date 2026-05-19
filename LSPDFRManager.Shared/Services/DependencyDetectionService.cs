using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Maps detected mod types to the runtime dependencies they require.
/// Aggregates and deduplicates warnings across primary and secondary types.
/// </summary>
public sealed class DependencyDetectionService : IDependencyDetectionService
{
    // ── Dependency catalog ────────────────────────────────────────────────────

    // Each entry: (dependency name, reason template, applicable types[])
    // Reason uses {type} as a placeholder for the originating ModType label.
    private static readonly (string Name, string Reason, ModType[] Types)[] Catalog =
    [
        (
            "Script Hook V",
            "Required by {type} — provides the native function hook that .asi mods and SHVDN depend on.",
            [ModType.AsiMod, ModType.Script]
        ),
        (
            "ASI Loader",
            "Required by {type} — loads .asi plugins from the GTA V root (Script Hook V includes one as dinput8.dll).",
            [ModType.AsiMod]
        ),
        (
            "ScriptHookVDotNet (SHVDN)",
            "Required by {type} — the .NET scripting bridge used by .cs/.vb scripts in the scripts/ folder.",
            [ModType.Script]
        ),
        (
            "LSPDFR",
            "Required by {type} — the law-enforcement mod framework that hosts LSPDFR plugin DLLs.",
            [ModType.LspdfrPlugin]
        ),
        (
            "RAGE Plugin Hook",
            "Required by {type} — the plugin host that LSPDFR and its plugins run on top of.",
            [ModType.LspdfrPlugin]
        ),
        (
            "OpenIV (or compatible OIV installer)",
            "Required by {type} — OIV packages must be applied through an OIV-aware installer; they cannot be extracted manually.",
            [ModType.OivPackage]
        ),
        (
            "EUP Menu / EUP for LSPDFR",
            "Required by {type} — the EUP framework that loads custom clothing packs into the game.",
            [ModType.Eup]
        ),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public DependencyDetectionResult Detect(ModTypeDetectionResult modTypeResult)
    {
        // Collect every type signal: primary + all secondaries above the
        // secondary threshold that was already applied by the detection service.
        var typesPresent = AllTypes(modTypeResult).ToList();

        if (typesPresent.Count == 0)
            return DependencyDetectionResult.Empty;

        // Emit one DependencyWarning per (dependency name × originating type) pair,
        // then deduplicate by dependency name so mixed archives don't double-warn.
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DependencyWarning>();

        foreach (var (name, reasonTemplate, applicableTypes) in Catalog)
        {
            foreach (var (type, label) in typesPresent)
            {
                if (!applicableTypes.Contains(type)) continue;
                if (!seen.Add(name)) break; // already emitted this dep from another type

                result.Add(new DependencyWarning
                {
                    Name       = name,
                    Reason     = reasonTemplate.Replace("{type}", label),
                    SourceType = type,
                });
                break; // one warning per dependency name is enough
            }
        }

        return new DependencyDetectionResult { Warnings = result };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<(ModType type, string label)> AllTypes(ModTypeDetectionResult result)
    {
        if (result.PrimaryType != ModType.Unknown)
            yield return (result.PrimaryType, TypeLabel(result.PrimaryType));

        foreach (var secondary in result.SecondaryTypes)
            yield return (secondary.Type, TypeLabel(secondary.Type));
    }

    private static string TypeLabel(ModType type) => type switch
    {
        ModType.AsiMod       => "ASI mod",
        ModType.Script       => "SHVDN script",
        ModType.LspdfrPlugin => "LSPDFR plugin",
        ModType.OivPackage   => "OIV package",
        ModType.Eup          => "EUP clothing pack",
        ModType.VehicleDlc   => "DLC pack",
        ModType.Map          => "map / MLO",
        ModType.Sound        => "sound pack",
        ModType.ConfigPreset => "config preset",
        _                    => type.ToString(),
    };
}
