using LSPDFRManager.Domain;

namespace LSPDFRManager.ViewModels;

/// <summary>
/// Pure filter and sort logic for the mod library.
/// Stateless — all inputs are passed as parameters.
/// Add new filter predicates or sort keys here without touching LibraryViewModel.
/// </summary>
internal static class LibraryFilter
{
    public static IEnumerable<InstalledMod> Apply(
        IEnumerable<InstalledMod> mods,
        string searchQuery,
        string typeFilter,
        string riskFilter,
        string sortKey,
        Func<string, IEnumerable<InstalledMod>> searchProvider)
    {
        if (!string.IsNullOrWhiteSpace(searchQuery.Trim()))
            mods = searchProvider(searchQuery.Trim());

        if (!typeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            mods = mods.Where(m => m.TypeLabel.Equals(typeFilter, StringComparison.OrdinalIgnoreCase));

        if (!riskFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            mods = mods.Where(m => RiskTier(m.DetectionScore).Equals(riskFilter, StringComparison.OrdinalIgnoreCase));

        return sortKey switch
        {
            "Installed: Oldest first" => mods.OrderBy(m => m.InstalledAt),
            "Name: A to Z"           => mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "Name: Z to A"           => mods.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "Author: A to Z"         => mods.OrderBy(m => m.Author, StringComparer.OrdinalIgnoreCase),
            "Enabled first"          => mods.OrderByDescending(m => m.IsEnabled).ThenByDescending(m => m.InstalledAt),
            "Load order"             => mods.OrderBy(m => m.LoadOrderPriority == 0 ? int.MaxValue : m.LoadOrderPriority).ThenBy(m => m.InstalledAt),
            _                        => mods.OrderByDescending(m => m.InstalledAt),
        };
    }

    public static string RiskTier(int score) =>
        score >= 70 ? "Safe" :
        score >= 40 ? "Medium" :
        "High";
}
