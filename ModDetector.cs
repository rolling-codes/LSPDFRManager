using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using LSPDFRManager.Core;
using LSPDFRManager.Models;
using SharpCompress.Archives;

namespace LSPDFRManager.Services;

public class ModDetector
{
    private record DetectionRule(
        ModType Type,
        string  Label,
        string  Color,
        string[] PathPatterns,
        string[] Extensions,
        string[] NameKeywords
    );

    private static readonly DetectionRule[] Rules =
    [
        new(ModType.VehicleDlc,     "Vehicle Add-On DLC",  "#F59E0B",
            ["dlc.rpf", "dlcpacks"],
            [".rpf"],
            ["dlcpack", "vehicle", "car", "addon"]),
        new(ModType.VehicleReplace, "Vehicle Replace",     "#EF4444",
            ["x64e.rpf", "replace", "_hi.ytd"],
            [".ytd", ".yft"],
            ["replace", "vehicle"]),
        new(ModType.LspdfrPlugin,   "LSPDFR Plugin",       "#3B82F6",
            ["plugins/lspdfr", "plugins\\lspdfr"],
            [".dll"],
            ["lspdfr", "plugin", "callout", "agency"]),
        new(ModType.AsiMod,         "ASI Mod",             "#8B5CF6",
            [],
            [".asi"],
            ["asi", "hook", "trainer"]),
        new(ModType.Script,         "Script (CS/VB)",      "#10B981",
            ["scripts"],
            [".cs", ".vb", ".lua"],
            ["script", "scripthook"]),
        new(ModType.Eup,            "EUP Clothing",        "#EC4899",
            ["eup", "componentpeds", "pedlayout"],
            [".ydd", ".ytd"],
            ["eup", "clothing", "uniform", "ped"]),
        new(ModType.Map,            "Map / MLO",           "#14B8A6",
            ["_mlo", "mlo", "map"],
            [".ymap", ".ytyp", ".ybn"],
            ["mlo", "map", "interior", "building"]),
        new(ModType.Sound,          "Sound Pack",          "#F97316",
            ["audio", "sound", "sfx"],
            [".awc", ".rel"],
            ["sound", "audio", "siren", "radio"]),
    ];

    public ModInfo Detect(string sourcePath)
    {
        AppLogger.Info($"Detecting: {Path.GetFileName(sourcePath)}");

        var files = ListFiles(sourcePath);
        var (rule, confidence) = Classify(files, Path.GetFileName(sourcePath));

        var info = new ModInfo
        {
            Name       = CleanName(Path.GetFileNameWithoutExtension(sourcePath)),
            Type       = rule?.Type    ?? ModType.Misc,
            TypeLabel  = rule?.Label   ?? "Miscellaneous",
            TypeColor  = rule?.Color   ?? "#6B7280",
            SourcePath = sourcePath,
            Files      = files,
            Confidence = confidence,
        };

        ExtractMetadata(info, files, sourcePath);
        AddWarnings(info);

        AppLogger.Info($"  → {info.TypeLabel} ({info.ConfidenceLabel} confidence)");
        return info;
    }

    private static (DetectionRule? rule, float confidence) Classify(List<string> files, string fileName)
    {
        var scores = new Dictionary<DetectionRule, float>();
        var nameLower = fileName.ToLowerInvariant();
        var joinedFiles = string.Join("\n", files);

        foreach (var rule in Rules)
        {
            float score = 0f;

            foreach (var pat in rule.PathPatterns)
                if (joinedFiles.Contains(pat, StringComparison.OrdinalIgnoreCase))
                    score += 0.5f;

            foreach (var ext in rule.Extensions)
                if (files.Any(f => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    score += 0.35f;

            foreach (var kw in rule.NameKeywords)
                if (nameLower.Contains(kw))
                    score += 0.15f;

            if (score > 0) scores[rule] = score;
        }

        if (scores.Count == 0) return (null, 0.1f);

        var best = scores.MaxBy(kv => kv.Value);
        float maxPossible = best.Key.PathPatterns.Length * 0.5f
                          + best.Key.Extensions.Length  * 0.35f
                          + best.Key.NameKeywords.Length * 0.15f;
        float confidence = maxPossible > 0 ? Math.Min(best.Value / maxPossible, 1f) : 0.5f;

        return (best.Key, confidence);
    }

    private List<string> ListFiles(string path)
    {
        try
        {
            if (Directory.Exists(path))
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Select(f => f.Replace('\\', '/').ToLowerInvariant())
                    .ToList();

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".zip")
                return ListZipEntries(path);

            return ListCompressedEntries(path);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Could not list files in {path}: {ex.Message}");
            return [Path.GetFileName(path).ToLowerInvariant()];
        }
    }

    private static List<string> ListZipEntries(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries
            .Select(e => e.FullName.Replace('\\', '/').ToLowerInvariant())
            .ToList();
    }

    private static List<string> ListCompressedEntries(string path)
    {
        try
        {
            using var archive = ArchiveFactory.Open(path);
            return archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e => (e.Key ?? string.Empty).Replace('\\', '/').ToLowerInvariant())
                .ToList();
        }
        catch
        {
            return [Path.GetFileNameWithoutExtension(path).ToLowerInvariant()];
        }
    }

    private static void ExtractMetadata(ModInfo info, List<string> files, string sourcePath)
    {
        var versionMatch = Regex.Match(Path.GetFileName(sourcePath),
            @"v?(\d+[\.\d]+)", RegexOptions.IgnoreCase);
        if (versionMatch.Success) info.Version = versionMatch.Groups[1].Value;

        if (info.Type == ModType.VehicleDlc)
        {
            info.IsAddon = files.Any(f => f.Contains("dlc.rpf"));
            info.DlcPackName = ExtractDlcPackName(files) ?? info.Name.ToLowerInvariant().Replace(" ", "_");
        }
        else if (info.Type == ModType.VehicleReplace)
        {
            info.IsAddon = false;
        }
    }

    private static string? ExtractDlcPackName(List<string> files)
    {
        foreach (var file in files)
        {
            var parts = file.Split('/');
            var idx = Array.IndexOf(parts, "dlcpacks");
            if (idx >= 0 && idx + 1 < parts.Length && !string.IsNullOrEmpty(parts[idx + 1]))
                return parts[idx + 1];
        }
        return null;
    }

    private static void AddWarnings(ModInfo info)
    {
        if (info.Confidence < 0.30f)
            info.Warnings.Add("Low detection confidence — verify mod type before installing");

        if (info.Type == ModType.VehicleDlc && string.IsNullOrEmpty(info.DlcPackName))
            info.Warnings.Add("Could not determine DLC pack name — you may need to set it manually");

        if (info.Type == ModType.Unknown || info.Type == ModType.Misc)
            info.Warnings.Add("Mod type could not be determined — files will be placed in GTA root");
    }

    private static string CleanName(string stem)
    {
        var name = Regex.Replace(stem, @"[_\-\.]+", " ");
        name = Regex.Replace(name, @"\bv\d[\d\.]*\b", "", RegexOptions.IgnoreCase);
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(name.Trim().ToLowerInvariant());
    }
}