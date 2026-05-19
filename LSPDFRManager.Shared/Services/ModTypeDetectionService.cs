using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Rule-based, evidence-producing mod-type classifier.
/// Pure function over normalized entry paths — no file I/O.
/// </summary>
public sealed class ModTypeDetectionService : IModTypeDetectionService
{
    // ── Thresholds ────────────────────────────────────────────────────────────

    // A type must reach this score fraction to be considered the primary candidate.
    private const float PrimaryThreshold = 0.30f;

    // A secondary type must reach this fraction to appear in SecondaryTypes.
    private const float SecondaryThreshold = 0.20f;

    // Primary and top-secondary within this gap → IsMixed = true.
    private const float MixedGap = 0.20f;

    // ── Rule definitions ─────────────────────────────────────────────────────

    private sealed class Rule
    {
        public required ModType Type         { get; init; }
        public required float   MaxScore     { get; init; }
        public required Func<IReadOnlyList<string>, string?, (float score, List<string> evidence)> Score { get; init; }
    }

    private static readonly Rule[] Rules =
    [
        // ── OIV Package ──────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.OivPackage,
            MaxScore = 1.0f,
            Score = (entries, _) =>
            {
                var ev = new List<string>();
                float s = 0f;

                // assembly.xml at root or one folder deep is the canonical, unambiguous OIV
                // marker — no other mod type uses it. Score at max so an OIV containing DLC
                // content (dlcpacks/, .rpf) is never misidentified as VehicleDlc.
                var asm = entries.FirstOrDefault(e =>
                    e == "assembly.xml" ||
                    (e.EndsWith("/assembly.xml") && e.Count(c => c == '/') == 1));
                if (asm is not null) { ev.Add($"Found OIV assembly manifest: {asm}"); s += 1.0f; }

                var oiv = entries.FirstOrDefault(e => e.EndsWith(".oiv"));
                if (oiv is not null) { ev.Add($"Found .oiv entry: {oiv}"); s += 0.5f; }

                return (s, ev);
            }
        },

        // ── ASI Mod ──────────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.AsiMod,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                var asiFiles = entries.Where(e => e.EndsWith(".asi")).ToList();
                if (asiFiles.Count > 0)
                {
                    s += 0.75f;
                    foreach (var f in asiFiles) ev.Add($"Found .asi file: {f}");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "asi", "trainer", "hook" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.10f; break; }

                return (s, ev);
            }
        },

        // ── ScriptHookVDotNet script ─────────────────────────────────────────
        new Rule
        {
            Type = ModType.Script,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                // Scripts placed in the scripts/ folder are definitive.
                var inScripts = entries.Where(e =>
                    (e.StartsWith("scripts/") || e.Contains("/scripts/")) &&
                    (e.EndsWith(".cs") || e.EndsWith(".vb") || e.EndsWith(".lua"))).ToList();
                if (inScripts.Count > 0)
                {
                    s += 0.80f;
                    foreach (var f in inScripts) ev.Add($"Script file in scripts/ folder: {f}");
                }

                // .lua anywhere is a SHVDN/FiveM signal even outside scripts/.
                var luas = entries.Where(e => e.EndsWith(".lua") && !inScripts.Contains(e)).ToList();
                if (luas.Count > 0)
                {
                    s += 0.40f;
                    foreach (var f in luas) ev.Add($"Found .lua script: {f}");
                }

                // .cs/.vb outside scripts/ folder — weaker signal (could be source).
                var looseScripts = entries.Where(e =>
                    (e.EndsWith(".cs") || e.EndsWith(".vb")) && !inScripts.Contains(e)).ToList();
                if (looseScripts.Count > 0)
                {
                    s += 0.20f;
                    foreach (var f in looseScripts.Take(2)) ev.Add($"Found script file (outside scripts/ folder): {f}");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "script", "scripthook" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.10f; break; }

                return (s, ev);
            }
        },

        // ── DLC Pack ─────────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.VehicleDlc,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                if (entries.Any(e => e.Contains("dlcpacks/")))
                {
                    s += 0.70f;
                    var dlcEntry = entries.First(e => e.Contains("dlcpacks/"));
                    ev.Add($"Found dlcpacks/ path: {dlcEntry}");
                }

                if (entries.Any(e => e.Contains("dlc.rpf")))
                {
                    s += 0.50f;
                    ev.Add("Found dlc.rpf manifest");
                }

                var rpfs = entries.Where(e => e.EndsWith(".rpf")).ToList();
                if (rpfs.Count > 0)
                {
                    s += 0.15f;
                    ev.Add($"Found {rpfs.Count} .rpf file(s)");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "dlcpack", "addon", "addonpack" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.15f; break; }

                return (s, ev);
            }
        },

        // ── LSPDFR Plugin ────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.LspdfrPlugin,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                var inLspdfr = entries.Where(e =>
                    (e.Contains("plugins/lspdfr/") || e.Contains("plugins\\lspdfr\\")) &&
                    e.EndsWith(".dll")).ToList();
                if (inLspdfr.Count > 0)
                {
                    s += 1.0f;
                    foreach (var f in inLspdfr) ev.Add($"DLL in plugins/lspdfr/: {f}");
                }

                var inPlugins = entries.Where(e =>
                    e.Contains("plugins/") && e.EndsWith(".dll") && !inLspdfr.Contains(e)).ToList();
                if (inPlugins.Count > 0)
                {
                    s += 0.55f;
                    foreach (var f in inPlugins) ev.Add($"DLL in plugins/ folder: {f}");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "lspdfr", "callout", "plugin", "agency" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.15f; break; }

                return (s, ev);
            }
        },

        // ── EUP Clothing ─────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.Eup,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                var eupPaths = entries.Where(e =>
                    e.Contains("eup/") || e.Contains("componentpeds/") || e.Contains("pedlayout")).ToList();
                if (eupPaths.Count > 0)
                {
                    s += 0.65f;
                    ev.Add($"Found EUP path pattern ({eupPaths.Count} entry/entries)");
                }

                var yddYtd = entries.Where(e => e.EndsWith(".ydd") || e.EndsWith(".ytd")).ToList();
                if (yddYtd.Count > 0)
                {
                    s += 0.30f;
                    ev.Add($"Found {yddYtd.Count} ped/clothing texture file(s) (.ydd/.ytd)");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "eup", "clothing", "uniform", "ped" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.15f; break; }

                return (s, ev);
            }
        },

        // ── Map / MLO ────────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.Map,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                var mapFiles = entries.Where(e =>
                    e.EndsWith(".ymap") || e.EndsWith(".ytyp") || e.EndsWith(".ybn")).ToList();
                if (mapFiles.Count > 0)
                {
                    s += 0.70f;
                    ev.Add($"Found {mapFiles.Count} map/MLO file(s) (.ymap/.ytyp/.ybn)");
                }

                if (entries.Any(e => e.Contains("_mlo") || e.Contains("/mlo") || e.Contains("/map")))
                {
                    s += 0.25f;
                    ev.Add("Found MLO/map path pattern");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "mlo", "map", "interior", "building" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.10f; break; }

                return (s, ev);
            }
        },

        // ── Sound Pack ───────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.Sound,
            MaxScore = 1.0f,
            Score = (entries, archiveName) =>
            {
                var ev = new List<string>();
                float s = 0f;

                var audioFiles = entries.Where(e => e.EndsWith(".awc") || e.EndsWith(".rel")).ToList();
                if (audioFiles.Count > 0)
                {
                    s += 0.70f;
                    ev.Add($"Found {audioFiles.Count} audio file(s) (.awc/.rel)");
                }

                if (entries.Any(e => e.Contains("audio/") || e.Contains("sound/") || e.Contains("sfx/")))
                {
                    s += 0.25f;
                    ev.Add("Found audio/ or sound/ path pattern");
                }

                var name = (archiveName ?? "").ToLowerInvariant();
                foreach (var kw in new[] { "sound", "siren", "audio", "radio" })
                    if (name.Contains(kw)) { ev.Add($"Archive name contains keyword '{kw}'"); s += 0.10f; break; }

                return (s, ev);
            }
        },

        // ── Config-only ──────────────────────────────────────────────────────
        new Rule
        {
            Type = ModType.ConfigPreset,
            MaxScore = 1.0f,
            Score = (entries, _) =>
            {
                var ev = new List<string>();

                // Only fire when every file is a config/text type — no binaries at all.
                var configExts  = new HashSet<string> { ".ini", ".xml", ".meta", ".cfg", ".json", ".txt", ".md", ".pdf" };
                var fileEntries = entries.Where(e => !e.EndsWith("/")).ToList();
                if (fileEntries.Count == 0) return (0f, ev);

                var nonConfig = fileEntries.Where(e =>
                {
                    var ext = Path.GetExtension(e);
                    return !string.IsNullOrEmpty(ext) && !configExts.Contains(ext);
                }).ToList();

                if (nonConfig.Count > 0) return (0f, ev);

                ev.Add($"Archive contains only configuration/text files ({fileEntries.Count} file(s))");
                var extsFound = fileEntries.Select(e => Path.GetExtension(e)).Distinct().Order();
                ev.Add($"Extensions present: {string.Join(", ", extsFound)}");
                return (0.90f, ev);
            }
        },
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public ModTypeDetectionResult Detect(IReadOnlyList<string> entryPaths, string? archiveName = null)
    {
        // Normalize: lowercase + forward slash.
        var entries = entryPaths
            .Select(p => p.Replace('\\', '/').ToLowerInvariant())
            .ToList();

        // Score every rule.
        var scored = new List<(ModType type, float confidence, List<string> evidence)>();
        foreach (var rule in Rules)
        {
            var (raw, ev) = rule.Score(entries, archiveName);
            if (raw <= 0f) continue;
            float conf = Math.Min(raw / rule.MaxScore, 1f);
            scored.Add((rule.Type, conf, ev));
        }

        scored.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        // Unknown result — nothing fired above primary threshold.
        if (scored.Count == 0 || scored[0].confidence < PrimaryThreshold)
        {
            var warnings = new List<string> { "No known mod-type pattern recognized — archive contents are unclassified." };
            return new ModTypeDetectionResult
            {
                PrimaryType = ModType.Unknown,
                Confidence  = scored.Count > 0 ? scored[0].confidence : 0f,
                Evidence    = [],
                Warnings    = warnings,
            };
        }

        var primary   = scored[0];
        var secondary = scored.Skip(1)
            .Where(s => s.confidence >= SecondaryThreshold)
            .Select(s => new DetectedModType(s.type, s.confidence, s.evidence))
            .ToList();

        bool isMixed = secondary.Count > 0 &&
                       (primary.confidence - secondary[0].Confidence) < MixedGap;

        var resultWarnings = BuildWarnings(primary.type, primary.confidence, isMixed, secondary);

        return new ModTypeDetectionResult
        {
            PrimaryType    = primary.type,
            Confidence     = primary.confidence,
            Evidence       = primary.evidence,
            SecondaryTypes = secondary,
            Warnings       = resultWarnings,
            IsMixed        = isMixed,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> BuildWarnings(
        ModType primary,
        float   confidence,
        bool    isMixed,
        IReadOnlyList<DetectedModType> secondary)
    {
        var w = new List<string>();

        if (isMixed)
        {
            var typeNames = string.Join(" and ",
                new[] { primary }.Concat(secondary.Take(1).Select(s => s.Type))
                    .Select(t => t.ToString()));
            w.Add($"Archive contains signals for multiple types ({typeNames}) — verify the intended type before installing.");
        }
        else if (confidence < 0.45f)
        {
            w.Add("Low detection confidence — verify mod type before installing.");
        }

        return w;
    }
}
