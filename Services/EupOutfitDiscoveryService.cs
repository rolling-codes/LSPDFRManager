using System.Xml.Linq;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Discovers EUP uniform/outfit definitions from locally installed config files
/// under the GTA root. Supports INI and XML formats. Never throws on bad files.
/// </summary>
public class EupOutfitDiscoveryService
{
    private readonly string _gtaPath;

    public EupOutfitDiscoveryService(string gtaPath) => _gtaPath = gtaPath;

    public List<EupUniformDefinition> Discover()
    {
        if (string.IsNullOrWhiteSpace(_gtaPath) || !Directory.Exists(_gtaPath))
            return [];

        var results = new List<EupUniformDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ScanDir(string relDir, string filePattern)
        {
            var dir = Path.Combine(_gtaPath, relDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) return;
            try
            {
                foreach (var file in Directory.GetFiles(dir, filePattern, SearchOption.AllDirectories))
                {
                    if (!seen.Add(Path.GetFullPath(file))) continue;
                    ParseFile(file, results);
                }
            }
            catch (Exception ex)
            {
                Core.AppLogger.Warning($"EupOutfitDiscoveryService: scan error in '{relDir}': {ex.Message}");
            }
        }

        ScanDir("plugins/EUP", "*.ini");
        ScanDir("plugins/EUP", "*.xml");
        ScanDir("Plugins/EUP", "*.ini");
        ScanDir("Plugins/EUP", "*.xml");
        ScanDir("plugins/lspdfr/EUP Menu", "*.ini");
        ScanDir("plugins/lspdfr/EUP Menu", "*.xml");
        ScanDir("plugins/lspdfr", "*eup*.ini");
        ScanDir("plugins/lspdfr", "*eup*.xml");
        ScanDir("lspdfr/data", "*.ini");
        ScanDir("lspdfr/data", "*.xml");
        ScanDir("lspdfr/data/custom", "*.ini");
        ScanDir("lspdfr/data/custom", "*.xml");
        ScanDir("LSPDFR/data", "*.ini");
        ScanDir("LSPDFR/data", "*.xml");
        ScanDir("LSPDFR/data/custom", "*.ini");
        ScanDir("LSPDFR/data/custom", "*.xml");
        ScanDir("EUP", "*.ini");
        ScanDir("EUP", "*.xml");

        // Specific well-known preset file locations
        foreach (var candidate in new[]
        {
            Path.Combine(_gtaPath, "plugins", "lspdfr", "presetoutfits.ini"),
            Path.Combine(_gtaPath, "plugins", "EUP", "presetoutfits.ini"),
            Path.Combine(_gtaPath, "plugins", "lspdfr", "EUP Menu", "presetoutfits.ini"),
        })
        {
            if (File.Exists(candidate) && seen.Add(Path.GetFullPath(candidate)))
                ParseFile(candidate, results);
        }

        return results;
    }

    private void ParseFile(string filePath, List<EupUniformDefinition> results)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            if (ext == ".ini")
                results.AddRange(ParseIni(filePath));
            else if (ext == ".xml")
                results.AddRange(ParseXml(filePath));
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"EupOutfitDiscoveryService: parse error for '{filePath}': {ex.Message}");
        }
    }

    // ── INI parsing ───────────────────────────────────────────────────────────
    // Sections become outfits. Keys: PedModel, Component*, Hat, etc.
    //
    // [SAHP Class A]
    // PedModel=mp_m_freemode_01
    // Component1=5,0

    private List<EupUniformDefinition> ParseIni(string filePath)
    {
        var results = new List<EupUniformDefinition>();
        string rel = RelPath(filePath);

        string? currentSection = null;
        string? pedModel = null;
        var components = new Dictionary<string, string>();
        var props = new Dictionary<string, string>();
        var meta = new Dictionary<string, string> { ["Format"] = "KnownIni" };
        bool hasAnyComponent = false;

        void FlushSection()
        {
            if (currentSection is null) return;
            var (dept, county, region) = Infer(currentSection, rel);
            var gender = EupInferenceHelper.InferGender(pedModel, currentSection, rel);
            bool isFreemode = EupInferenceHelper.IsFreemodePed(pedModel);
            float confidence = isFreemode ? 0.85f : hasAnyComponent ? 0.5f : 0.3f;
            meta["Supported"] = isFreemode ? "true" : "false";

            results.Add(new EupUniformDefinition
            {
                DisplayName = currentSection,
                Department = dept,
                County = county,
                Region = region,
                Gender = gender,
                PedModel = pedModel,
                SourceRelativePath = rel,
                Components = new Dictionary<string, string>(components),
                Props = new Dictionary<string, string>(props),
                Metadata = new Dictionary<string, string>(meta),
                Confidence = confidence,
            });
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(';') || line.StartsWith('#') || line.Length == 0) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                FlushSection();
                currentSection = line[1..^1].Trim();
                pedModel = null;
                components = [];
                props = [];
                hasAnyComponent = false;
                meta = new Dictionary<string, string> { ["Format"] = "KnownIni" };
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0 || currentSection is null) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (key.Equals("PedModel", StringComparison.OrdinalIgnoreCase))
                pedModel = value;
            else if (key.StartsWith("Prop", StringComparison.OrdinalIgnoreCase))
            {
                props[key] = value;
                hasAnyComponent = true;
            }
            else if (key.StartsWith("Component", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Hat", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Mask", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Torso", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Legs", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Shoes", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Accessory", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Top", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Undershirt", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Badge", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("Texture", StringComparison.OrdinalIgnoreCase))
            {
                components[key] = value;
                hasAnyComponent = true;
            }
            else
            {
                meta[key] = value;
            }
        }

        FlushSection();
        return results;
    }

    // ── XML parsing ───────────────────────────────────────────────────────────
    // <Outfit name="SAHP Class A" pedModel="mp_m_freemode_01">
    //   <Component id="1" drawable="5" texture="0"/>
    // </Outfit>

    private List<EupUniformDefinition> ParseXml(string filePath)
    {
        var results = new List<EupUniformDefinition>();
        string rel = RelPath(filePath);

        XDocument doc;
        try { doc = XDocument.Load(filePath); }
        catch { return results; }

        var outfitElements = doc.Descendants()
            .Where(e => e.Name.LocalName.Equals("Outfit", StringComparison.OrdinalIgnoreCase)
                     || e.Name.LocalName.Equals("Uniform", StringComparison.OrdinalIgnoreCase)
                     || e.Name.LocalName.Equals("Preset", StringComparison.OrdinalIgnoreCase));

        foreach (var el in outfitElements)
        {
            var displayName = el.Attribute("name")?.Value
                           ?? el.Attribute("Name")?.Value
                           ?? el.Attribute("id")?.Value
                           ?? el.Name.LocalName;

            var pedModel = el.Attribute("pedModel")?.Value
                        ?? el.Attribute("PedModel")?.Value
                        ?? el.Attribute("ped")?.Value;

            var components = new Dictionary<string, string>();
            foreach (var comp in el.Elements())
            {
                var id = comp.Attribute("id")?.Value ?? comp.Name.LocalName;
                var drawable = comp.Attribute("drawable")?.Value ?? "";
                var texture = comp.Attribute("texture")?.Value ?? "";
                components[$"{comp.Name.LocalName}_{id}"] = $"{drawable},{texture}";
            }

            var (dept, county, region) = Infer(displayName, rel);
            var gender = EupInferenceHelper.InferGender(pedModel, displayName, rel);
            bool isFreemode = EupInferenceHelper.IsFreemodePed(pedModel);
            float confidence = isFreemode ? 0.8f : components.Count > 0 ? 0.45f : 0.3f;

            results.Add(new EupUniformDefinition
            {
                DisplayName = displayName,
                Department = dept,
                County = county,
                Region = region,
                Gender = gender,
                PedModel = pedModel,
                SourceRelativePath = rel,
                Components = components,
                Metadata = new Dictionary<string, string>
                {
                    ["Format"] = "KnownXml",
                    ["Supported"] = isFreemode ? "true" : "false",
                },
                Confidence = confidence,
            });
        }

        return results;
    }

    private static (string Department, string County, string Region) Infer(string name, string path)
    {
        var dept = EupInferenceHelper.InferDepartment(name, path);
        var (county, region) = EupInferenceHelper.InferCountyAndRegion(name, path);
        return (dept, county, region);
    }

    private string RelPath(string absolute) =>
        string.IsNullOrEmpty(_gtaPath) ? absolute : Path.GetRelativePath(_gtaPath, absolute);
}
