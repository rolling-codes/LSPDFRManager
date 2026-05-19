using LSPDFRManager.Domain;
using System.Text.RegularExpressions;

namespace LSPDFRManager.Services;

public static class EupInferenceHelper
{
    private static readonly Regex FemaleTokenRegex = new(
        @"(^|[^A-Za-z0-9])(female|f_[A-Za-z0-9]+)([^A-Za-z0-9]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MaleTokenRegex = new(
        @"(^|[^A-Za-z0-9])(male|m_[A-Za-z0-9]+)([^A-Za-z0-9]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Freemode ped detection ────────────────────────────────────────────────

    public static bool IsFreemodePed(string? pedModel) =>
        pedModel?.Equals("mp_m_freemode_01", StringComparison.OrdinalIgnoreCase) == true
        || pedModel?.Equals("mp_f_freemode_01", StringComparison.OrdinalIgnoreCase) == true;

    public static EupGender InferGenderFromPedModel(string? pedModel)
    {
        if (pedModel?.Equals("mp_m_freemode_01", StringComparison.OrdinalIgnoreCase) == true)
            return EupGender.Male;
        if (pedModel?.Equals("mp_f_freemode_01", StringComparison.OrdinalIgnoreCase) == true)
            return EupGender.Female;
        return EupGender.Unknown;
    }

    // ── Department inference ──────────────────────────────────────────────────

    // Checks specific multi-word phrases before single-word aliases to prevent
    // "blaine county sheriff" from matching LSSD, and "state police" from matching
    // plain "police". Order is load-bearing — do not reorder.
    public static string InferDepartment(string name, string folderPath)
    {
        var text = $"{name} {folderPath}";

        if (ContainsPhrase(text, "blaine county sheriff")) return "BCSO";
        if (ContainsPhrase(text, "bcso")) return "BCSO";
        if (ContainsPhrase(text, "los santos sheriff")) return "LSSD";
        if (ContainsPhrase(text, "lssd")) return "LSSD";
        // Bare "sheriff" is ambiguous — not BCSO, not LSSD
        if (ContainsWord(text, "sheriff")) return "Sheriff/Unknown";

        if (ContainsPhrase(text, "state police")) return "SAHP";
        if (ContainsPhrase(text, "state trooper")) return "SAHP";
        if (ContainsPhrase(text, "highway patrol")) return "SAHP";
        if (ContainsPhrase(text, "sahp")) return "SAHP";
        if (ContainsWord(text, "trooper")) return "SAHP";

        if (ContainsPhrase(text, "park ranger")) return "Park Ranger";
        if (ContainsPhrase(text, "los santos police")) return "LSPD";
        if (ContainsPhrase(text, "police department")) return "LSPD";
        if (ContainsPhrase(text, "lspd")) return "LSPD";
        if (ContainsWord(text, "police")) return "LSPD";

        if (ContainsPhrase(text, "federal investigation")) return "FIB";
        if (ContainsPhrase(text, "fib")) return "FIB";
        if (ContainsWord(text, "federal")) return "FIB";

        if (ContainsPhrase(text, "swat")) return "SWAT";
        if (ContainsWord(text, "tactical")) return "SWAT";
        if (ContainsWord(text, "metro")) return "SWAT";

        if (ContainsWord(text, "ranger")) return "Park Ranger";
        if (ContainsWord(text, "paramedic")) return "Fire/EMS";
        if (ContainsWord(text, "ems")) return "Fire/EMS";
        if (ContainsWord(text, "fire")) return "Fire/EMS";

        return "Unknown";
    }

    // ── County + region inference ─────────────────────────────────────────────

    // Returns (County, Region). Specific towns imply Blaine County.
    public static (string County, string Region) InferCountyAndRegion(string name, string folderPath)
    {
        var text = $"{name} {folderPath}";

        if (ContainsPhrase(text, "paleto bay") || ContainsWord(text, "paleto"))
            return ("Blaine County", "Paleto Bay");
        if (ContainsPhrase(text, "sandy shores") || ContainsWord(text, "sandy"))
            return ("Blaine County", "Sandy Shores");
        if (ContainsWord(text, "grapeseed"))
            return ("Blaine County", "Grapeseed");
        if (ContainsPhrase(text, "blaine county") || ContainsWord(text, "blaine"))
            return ("Blaine County", "Blaine County");
        if (ContainsPhrase(text, "los santos") || ContainsPhrase(text, "ls "))
            return ("Los Santos", "Los Santos");
        if (ContainsWord(text, "statewide") || ContainsPhrase(text, "all regions"))
            return ("Statewide", "Statewide");

        return ("Unknown", "Unknown");
    }

    // Convenience wrapper for callers that only need county
    public static string InferCounty(string name, string folderPath) =>
        InferCountyAndRegion(name, folderPath).County;

    // ── Gender inference ──────────────────────────────────────────────────────

    public static EupGender InferGender(string? pedModel, string name, string folderPath)
    {
        var fromModel = InferGenderFromPedModel(pedModel);
        if (fromModel != EupGender.Unknown) return fromModel;

        var text = $"{name} {folderPath}";
        if (FemaleTokenRegex.IsMatch(text))
            return EupGender.Female;
        if (MaleTokenRegex.IsMatch(text))
            return EupGender.Male;

        return EupGender.Unknown;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool ContainsPhrase(string text, string phrase) =>
        text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    // Word boundary check: phrase must be surrounded by non-letter chars (or string edges)
    private static bool ContainsWord(string text, string word)
    {
        int idx = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        bool startOk = idx == 0 || !char.IsLetter(text[idx - 1]);
        bool endOk = idx + word.Length >= text.Length || !char.IsLetter(text[idx + word.Length]);
        return startOk && endOk;
    }
}
