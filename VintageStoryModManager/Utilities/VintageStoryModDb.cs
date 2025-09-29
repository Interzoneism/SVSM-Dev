using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace VintageStoryModManager.Utilities;

/// <summary>
/// Helper methods for retrieving mod metadata from the official Vintage Story mod database.
/// </summary>
internal static class VintageStoryModDb
{
    private static readonly Uri BaseUri = new("https://mods.vintagestory.at/");

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// Attempts to build a best-effort URL to a mod entry using slug heuristics.
    /// </summary>
    public static Uri? TryCreateEntryUri(string? modNameOrId)
    {
        if (string.IsNullOrWhiteSpace(modNameOrId))
        {
            return null;
        }

        string slug = Slugify(modNameOrId);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return new Uri(BaseUri, slug);
    }

    /// <summary>
    /// Finds the mod by name on mods.vintagestory.at and extracts:
    /// - latest compatible Vintage Story version
    /// - 1-click install link
    /// - latest mod version
    /// - tags
    ///
    /// Uses the public API when it can; falls back to HTML scraping if needed.
    /// </summary>
    public static async Task<ModDbEntryInfo?> GetModInfoAsync(string modName)
    {
        if (string.IsNullOrWhiteSpace(modName))
        {
            throw new ArgumentException("modName is empty.", nameof(modName));
        }

        JsonElement? best = null;
        try
        {
            string searchUrl = $"api/mods?text={Uri.EscapeDataString(modName)}";
            using HttpResponseMessage resp = await Http.GetAsync(searchUrl).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

                if (doc.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
                {
                    best = ChooseBestMatch(modName, data);
                }
            }
        }
        catch
        {
            // swallow + fall back
        }

        if (best is JsonElement hit)
        {
            (string idOrSlug, string pageUrl, string displayName) = ExtractModIdentity(hit);

            using JsonDocument? full = await TryFetchFullModFromApi(idOrSlug).ConfigureAwait(false);
            if (full is not null)
            {
                ModDbEntryInfo? info = ExtractFromApi(full, pageUrl, idOrSlug, displayName);
                if (info is not null)
                {
                    return info;
                }
            }

            if (!string.IsNullOrWhiteSpace(pageUrl))
            {
                ModDbEntryInfo? scraped = await ScrapeFromHtmlAsync(pageUrl, idOrSlug, displayName).ConfigureAwait(false);
                if (scraped is not null)
                {
                    return scraped;
                }
            }
        }

        string slug = Slugify(modName);
        string guessedUrl = new Uri(BaseUri, slug).ToString();
        ModDbEntryInfo? htmlFallback = await ScrapeFromHtmlAsync(guessedUrl, slug, modName).ConfigureAwait(false);
        if (htmlFallback is not null)
        {
            return htmlFallback;
        }

        string listSearchUrl = $"https://mods.vintagestory.at/list/mod?text={Uri.EscapeDataString(modName)}";
        return await ScrapeFromListThenPageAsync(listSearchUrl, modName).ConfigureAwait(false);
    }

    private static JsonElement? ChooseBestMatch(string query, JsonElement dataArray)
    {
        JsonElement? best = null;

        string q = query.Trim();
        bool IsExact(JsonElement e, string prop)
        {
            return e.TryGetProperty(prop, out JsonElement v) &&
                   string.Equals(v.GetString(), q, StringComparison.OrdinalIgnoreCase);
        }

        foreach (JsonElement e in dataArray.EnumerateArray())
        {
            if (IsExact(e, "name") || IsExact(e, "title") || IsExact(e, "modid"))
            {
                return e;
            }

            best ??= e;
        }

        return best;
    }

    private static (string idOrSlug, string pageUrl, string displayName) ExtractModIdentity(JsonElement e)
    {
        string idOrSlug =
            e.TryGetProperty("modid", out JsonElement mid) ? mid.GetString() ?? string.Empty :
            e.TryGetProperty("id", out JsonElement idVal) ? idVal.ToString() :
            string.Empty;

        string pageUrl =
            e.TryGetProperty("link", out JsonElement lnk) ? lnk.GetString() ?? string.Empty :
            (!string.IsNullOrWhiteSpace(idOrSlug) ? new Uri(BaseUri, idOrSlug).ToString() : string.Empty);

        string displayName =
            e.TryGetProperty("name", out JsonElement nm) ? nm.GetString() ?? string.Empty :
            e.TryGetProperty("title", out JsonElement tt) ? tt.GetString() ?? string.Empty :
            idOrSlug;

        return (idOrSlug, pageUrl, displayName);
    }

    private static async Task<JsonDocument?> TryFetchFullModFromApi(string idOrSlug)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return null;
        }

        try
        {
            using HttpResponseMessage resp = await Http.GetAsync($"api/mod/{Uri.EscapeDataString(idOrSlug)}").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static ModDbEntryInfo? ExtractFromApi(JsonDocument full, string? pageUrl, string? idOrSlug, string? displayName)
    {
        try
        {
            JsonElement root = full.RootElement;
            if (!root.TryGetProperty("data", out JsonElement data))
            {
                return null;
            }

            List<JsonElement> releases = data.TryGetProperty("releases", out JsonElement relArr) && relArr.ValueKind == JsonValueKind.Array
                ? relArr.EnumerateArray().ToList()
                : new List<JsonElement>();

            JsonElement? latest = releases.FirstOrDefault();

            string latestVersion = TryGetString(latest, "version");
            string latestGameVersion = TryGetString(latest, "gameversion", "gameVersion", "forgameversion", "for_gameversion");
            string oneClickUrl = TryGetString(latest, "installurl", "oneclickinstall", "oneclickurl", "installUrl");

            var tags = new List<string>();
            if (data.TryGetProperty("asset", out JsonElement asset))
            {
                if (asset.TryGetProperty("tags", out JsonElement tagArr) && tagArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement t in tagArr.EnumerateArray())
                    {
                        string? tagName =
                            t.ValueKind == JsonValueKind.Object && t.TryGetProperty("name", out JsonElement tn) ? tn.GetString() :
                            t.ValueKind == JsonValueKind.String ? t.GetString() :
                            null;
                        if (!string.IsNullOrWhiteSpace(tagName))
                        {
                            tags.Add(tagName);
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(oneClickUrl) && !string.IsNullOrWhiteSpace(idOrSlug) && !string.IsNullOrWhiteSpace(latestVersion))
            {
                oneClickUrl = $"{idOrSlug}@{latestVersion}";
            }

            return new ModDbEntryInfo
            {
                ModPageUrl = string.IsNullOrWhiteSpace(pageUrl) ? new Uri(BaseUri, idOrSlug ?? string.Empty).ToString() : pageUrl,
                ModIdOrSlug = idOrSlug ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? (idOrSlug ?? string.Empty) : displayName!,
                LatestCompatibleGameVersion = latestGameVersion,
                OneClickInstallUrl = oneClickUrl,
                LatestModVersion = latestVersion,
                Tags = tags
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ModDbEntryInfo?> ScrapeFromHtmlAsync(string modPageUrl, string idOrSlug, string displayName)
    {
        try
        {
            string requestPath = modPageUrl.StartsWith(BaseUri.ToString(), StringComparison.OrdinalIgnoreCase)
                ? modPageUrl[BaseUri.ToString().Length..]
                : modPageUrl;

            string html = await Http.GetStringAsync(requestPath).ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tags = new List<string>();
            HtmlNodeCollection? tagAnchorNodes = doc.DocumentNode
                .SelectNodes("//a[starts-with(normalize-space(text()), '#')]");
            if (tagAnchorNodes != null)
            {
                foreach (HtmlNode a in tagAnchorNodes)
                {
                    string t = a.InnerText.Trim().TrimStart('#');
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        tags.Add(t);
                    }
                }
            }

            string latestVsVersion = string.Empty;
            HtmlNode? latestReleaseHeading = doc.DocumentNode.SelectSingleNode("//*[contains(text(),'Latest release (for Vintage Story')]");
            if (latestReleaseHeading != null)
            {
                Match m = Regex.Match(latestReleaseHeading.InnerText, @"Latest release\s*\(for Vintage Story\s*([^)]+)\)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    latestVsVersion = m.Groups[1].Value.Trim();
                }
            }

            string oneClick = string.Empty;
            HtmlNode? oneClickNode = doc.DocumentNode.SelectSingleNode("//a[contains(., '1-click install')]");
            if (oneClickNode != null)
            {
                oneClick = oneClickNode.GetAttributeValue("href", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(oneClick))
                {
                    string text = oneClickNode.InnerText ?? string.Empty;
                    Match m = Regex.Match(text, @"([a-z0-9\-_]+)@([\w\.\-]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        oneClick = $"{m.Groups[1].Value}@{m.Groups[2].Value}";
                    }
                }
            }

            string latestModVersion = string.Empty;
            if (oneClickNode?.ParentNode?.ParentNode is { } rows)
            {
                string text = rows.InnerText ?? string.Empty;
                Match verMatch = Regex.Match(text, @"\b(\d+\.\d+(?:\.\d+)?)\b");
                if (verMatch.Success)
                {
                    latestModVersion = verMatch.Groups[1].Value;
                }
            }

            if (string.IsNullOrWhiteSpace(latestModVersion) && !string.IsNullOrWhiteSpace(oneClick))
            {
                Match m2 = Regex.Match(oneClick, @"@([\w\.\-]+)$");
                if (m2.Success)
                {
                    latestModVersion = m2.Groups[1].Value;
                }
            }

            return new ModDbEntryInfo
            {
                ModPageUrl = modPageUrl,
                ModIdOrSlug = idOrSlug,
                DisplayName = displayName,
                LatestCompatibleGameVersion = latestVsVersion,
                OneClickInstallUrl = oneClick,
                LatestModVersion = latestModVersion,
                Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ModDbEntryInfo?> ScrapeFromListThenPageAsync(string listUrl, string query)
    {
        try
        {
            string requestPath = listUrl.StartsWith(BaseUri.ToString(), StringComparison.OrdinalIgnoreCase)
                ? listUrl[BaseUri.ToString().Length..]
                : listUrl;

            string html = await Http.GetStringAsync(requestPath).ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNodeCollection? anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors == null)
            {
                return null;
            }

            var best = anchors
                .Select(a => new { Node = a, Href = a.GetAttributeValue("href", string.Empty) })
                .Where(x => x.Href.StartsWith("/") && !x.Href.StartsWith("//"))
                .OrderByDescending(x => (AnchorContains(x.Node, query) ? 3 : 0) + (x.Href.Contains(Slugify(query)) ? 1 : 0))
                .FirstOrDefault();

            if (best == null)
            {
                return null;
            }

            string slug = best.Href.Trim('/').Split('/').Last();
            string fullUrl = new Uri(BaseUri, best.Href).ToString();
            return await ScrapeFromHtmlAsync(fullUrl, slug, query).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        static bool AnchorContains(HtmlNode a, string s)
            => (a.InnerText ?? string.Empty).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Slugify(string s)
    {
        string slug = s.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\-]+", "-");
        slug = Regex.Replace(slug, "-+", "-").Trim('-');
        return slug;
    }

    private static string TryGetString(JsonElement? element, params string[] propertyNames)
    {
        if (element is not JsonElement el || el.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (string property in propertyNames)
        {
            if (el.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = BaseUri
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VintageStoryModManager/1.0");
        return client;
    }

    public sealed class ModDbEntryInfo
    {
        public string ModPageUrl { get; init; } = string.Empty;
        public string ModIdOrSlug { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string LatestCompatibleGameVersion { get; init; } = string.Empty;
        public string OneClickInstallUrl { get; init; } = string.Empty;
        public string LatestModVersion { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = new();
    }
}
