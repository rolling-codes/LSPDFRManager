using System.Text.RegularExpressions;

namespace LSPDFRManager.Services;

public static class LspdfrThumbnailExtractor
{
    private const int CacheLimit = 256;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheOrder = new();

    private static readonly Regex MetaTagRegex = new(
        @"<meta\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*[""'](?<value>[^""']*)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FirstImgRegex = new(
        @"<img[^>]*src\s*=\s*[""'](?<u>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static string? ExtractThumbnailUrl(string pageUrl, string html)
    {
        if (string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(html))
            return null;

        if (TryGetCached(pageUrl, out var cached))
            return cached;

        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
            return null;

        var candidates = new[]
        {
            MatchMeta(html, "property", "og:image"),
            MatchMeta(html, "name", "twitter:image"),
            MatchFirstImg(html),
        };

        foreach (var candidate in candidates)
        {
            var safe = ResolveAndValidate(baseUri, candidate);
            if (safe is not null)
            {
                SetCached(pageUrl, safe);
                return safe;
            }
        }

        SetCached(pageUrl, null);
        return null;
    }

    private static string? MatchMeta(string html, string attrName, string attrValue)
    {
        foreach (Match metaTag in MetaTagRegex.Matches(html))
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attr in AttributeRegex.Matches(metaTag.Value))
            {
                var name = attr.Groups["name"].Value;
                var value = attr.Groups["value"].Value;
                if (!string.IsNullOrWhiteSpace(name))
                    attrs[name] = value;
            }

            if (attrs.TryGetValue(attrName, out var found) &&
                found.Equals(attrValue, StringComparison.OrdinalIgnoreCase) &&
                attrs.TryGetValue("content", out var content))
            {
                return content.Trim();
            }
        }

        return null;
    }

    private static string? MatchFirstImg(string html)
    {
        var m = FirstImgRegex.Match(html);
        return m.Success ? m.Groups["u"].Value.Trim() : null;
    }

    private static bool TryGetCached(string pageUrl, out string? cached)
    {
        lock (CacheLock)
        {
            return Cache.TryGetValue(pageUrl, out cached);
        }
    }

    private static void SetCached(string pageUrl, string? value)
    {
        lock (CacheLock)
        {
            if (!Cache.ContainsKey(pageUrl))
            {
                CacheOrder.Enqueue(pageUrl);
            }

            Cache[pageUrl] = value;

            while (Cache.Count > CacheLimit && CacheOrder.Count > 0)
            {
                var oldest = CacheOrder.Dequeue();
                Cache.Remove(oldest);
            }
        }
    }

    private static string? ResolveAndValidate(Uri baseUri, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!Uri.TryCreate(baseUri, raw, out var uri))
            return null;

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return null;

        if (uri.IsLoopback)
            return null;

        var host = uri.Host.ToLowerInvariant();
        if (!(host == "lcpdfr.com" || host.EndsWith(".lcpdfr.com") || host == "lspdfr.com" || host.EndsWith(".lspdfr.com")))
            return null;

        return uri.ToString();
    }
}
