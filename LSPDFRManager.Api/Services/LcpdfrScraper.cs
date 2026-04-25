using HtmlAgilityPack;
using LSPDFRManager.Api.Models;

namespace LSPDFRManager.Api.Services;

/// <summary>
/// Scrapes lcpdfr.com (Invision Community) to expose mod data as clean JSON.
/// All selectors are centralised here so they're easy to update if the site's
/// HTML structure changes.
/// </summary>
public class LcpdfrScraper
{
    private const string BaseUrl    = "https://www.lcpdfr.com";
    private const string SearchPath = "/search/?q={0}&type=core_downloads&search_and_or=or&sortby=relevancy";
    private const string BrowsePath = "/files/";

    private readonly HttpClient _http;

    public LcpdfrScraper(HttpClient http) => _http = http;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Searches lcpdfr.com for mods matching <paramref name="query"/>.
    /// </summary>
    public async Task<List<ModSummary>> SearchAsync(string query,
        string? category = null, CancellationToken ct = default)
    {
        var url  = BaseUrl + string.Format(SearchPath, Uri.EscapeDataString(query));
        var html = await FetchAsync(url, ct).ConfigureAwait(false);
        if (html is null) return [];

        var doc  = new HtmlDocument();
        doc.LoadHtml(html);

        // IPS search results live in <li data-role="activityItem"> or <article> blocks
        var items = doc.DocumentNode.SelectNodes(
            "//li[contains(@class,'ipsStreamItem')]|//article[contains(@class,'ipsStreamItem')]")
            ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'ipsDataItem')]");

        if (items is null) return [];

        var results = new List<ModSummary>();
        foreach (var item in items.Take(30))
        {
            var summary = ParseSearchItem(item);
            if (summary is not null) results.Add(summary);
        }
        return results;
    }

    /// <summary>
    /// Fetches the detail page for a specific mod identified by its URL slug or numeric ID.
    /// </summary>
    public async Task<ModSummary?> GetModAsync(string idOrSlug, CancellationToken ct = default)
    {
        // Accept either a full URL or a bare id/slug
        var url  = idOrSlug.StartsWith("http") ? idOrSlug
                 : $"{BaseUrl}/files/file/{idOrSlug}/";
        var html = await FetchAsync(url, ct).ConfigureAwait(false);
        if (html is null) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ParseDetailPage(doc, url);
    }

    // ── Parsing ──────────────────────────────────────────────────────────────

    private static ModSummary? ParseSearchItem(HtmlNode item)
    {
        // Title / URL
        var titleNode = item.SelectSingleNode(".//h2[contains(@class,'ipsStreamItem_title')]//a")
                      ?? item.SelectSingleNode(".//span[contains(@class,'ipsContained')]//a")
                      ?? item.SelectSingleNode(".//a[@data-ipstooltip]");
        if (titleNode is null) return null;

        var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
        var href  = titleNode.GetAttributeValue("href", "");
        var url   = href.StartsWith("http") ? href : BaseUrl + href;

        // Derive numeric id from URL: /files/file/12345-name/ → "12345"
        var id = ExtractNumericId(url);

        // Author
        var authorNode = item.SelectSingleNode(".//a[contains(@class,'ipsType_blendLinks')]")
                       ?? item.SelectSingleNode(".//span[@data-member-id]");
        var author = HtmlEntity.DeEntitize(authorNode?.InnerText.Trim() ?? "");

        // Version badge
        var versionNode = item.SelectSingleNode(".//*[contains(@class,'ipsBadge')]")
                        ?? item.SelectSingleNode(".//*[@data-version]");
        var version = versionNode?.InnerText.Trim() ?? "";

        // Description
        var descNode = item.SelectSingleNode(".//div[contains(@class,'ipsStreamItem_snippet')]")
                     ?? item.SelectSingleNode(".//p");
        var description = HtmlEntity.DeEntitize(descNode?.InnerText.Trim() ?? "");

        // Thumbnail
        var imgNode = item.SelectSingleNode(".//img[@src]");
        var image   = imgNode?.GetAttributeValue("src", "") ?? "";

        // Category → mod type label
        var catNode = item.SelectSingleNode(".//*[contains(@class,'ipsDataItem_meta')]");
        var type    = MapCategory(catNode?.InnerText.Trim() ?? "");

        // Updated date
        var timeNode = item.SelectSingleNode(".//time[@datetime]");
        var updated  = timeNode?.GetAttributeValue("datetime", "") ?? "";

        return new ModSummary(id, title, author, version, type,
            description, url, "", image, updated);
    }

    private static ModSummary? ParseDetailPage(HtmlDocument doc, string url)
    {
        var id    = ExtractNumericId(url);
        var title = doc.DocumentNode
                       .SelectSingleNode("//h1[contains(@class,'ipsType_pageTitle')]")
                       ?.InnerText.Trim() ?? "";
        title = HtmlEntity.DeEntitize(title);

        var author = doc.DocumentNode
                        .SelectSingleNode("//a[@data-member-id]")
                        ?.InnerText.Trim() ?? "";

        var version = doc.DocumentNode
                         .SelectSingleNode("//*[contains(@class,'ipsBadge')]")
                         ?.InnerText.Trim() ?? "";

        var description = doc.DocumentNode
                             .SelectSingleNode("//*[contains(@class,'ipsType_richText')]")
                             ?.InnerText.Trim() ?? "";
        description = HtmlEntity.DeEntitize(description);

        // Download button href
        var dlNode = doc.DocumentNode
                        .SelectSingleNode("//a[contains(@class,'ipsButton_primary') and contains(@href,'do=download')]");
        var downloadUrl = dlNode is null ? ""
            : (dlNode.GetAttributeValue("href", "").StartsWith("http")
                ? dlNode.GetAttributeValue("href", "")
                : BaseUrl + dlNode.GetAttributeValue("href", ""));

        var image = doc.DocumentNode
                       .SelectSingleNode("//div[contains(@class,'ipsFiles_image')]//img")
                       ?.GetAttributeValue("src", "") ?? "";

        var type = MapCategory(
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'ipsBreadcrumb')]//li[last()]")
               ?.InnerText.Trim() ?? "");

        return new ModSummary(id, title, author, version, type,
            description, url, downloadUrl, image, "");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            // Polite browser-like UA so we don't get blocked
            req.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch { return null; }
    }

    private static string ExtractNumericId(string url)
    {
        // /files/file/12345-some-name/ → "12345"
        var seg = url.TrimEnd('/').Split('/').LastOrDefault() ?? "";
        var dash = seg.IndexOf('-');
        return dash > 0 ? seg[..dash] : seg;
    }

    private static string MapCategory(string raw) => raw.ToLowerInvariant() switch
    {
        var s when s.Contains("script") || s.Contains("plugin") => "LSPDFR Plugin",
        var s when s.Contains("vehicle")                         => "Vehicle Add-On DLC",
        var s when s.Contains("eup")                             => "EUP Clothing",
        var s when s.Contains("map") || s.Contains("mlo")        => "Map / MLO",
        var s when s.Contains("sound") || s.Contains("audio")    => "Sound Pack",
        _                                                         => "Miscellaneous",
    };
}
