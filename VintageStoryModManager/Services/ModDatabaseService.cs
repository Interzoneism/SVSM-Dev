using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Retrieves additional metadata for installed mods from the Vintage Story mod database.
/// </summary>
public sealed class ModDatabaseService
{
    private const string ApiEndpointFormat = "https://mods.vintagestory.at/api/mod/{0}";
    private const string SearchEndpointFormat = "https://mods.vintagestory.at/api/mods?search={0}&limit={1}";
    private const string ModPageBaseUrl = "https://mods.vintagestory.at/show/mod/";

    private static readonly HttpClient HttpClient = new();

    private static readonly Regex HtmlBreakRegex = new(
        @"<\s*br\s*/?\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlParagraphOpenRegex = new(
        @"<\s*p[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlParagraphCloseRegex = new(
        @"</\s*p\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlListItemOpenRegex = new(
        @"<\s*li[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlListItemCloseRegex = new(
        @"</\s*li\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlBlockCloseRegex = new(
        @"</\s*(div|section|article|h[1-6])\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task PopulateModDatabaseInfoAsync(IEnumerable<ModEntry> mods, string? installedGameVersion, CancellationToken cancellationToken = default)
    {
        if (mods is null)
        {
            throw new ArgumentNullException(nameof(mods));
        }

        string? normalizedGameVersion = VersionStringUtility.Normalize(installedGameVersion);

        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mod is null || string.IsNullOrWhiteSpace(mod.ModId))
            {
                continue;
            }

            ModDatabaseInfo? info = await TryLoadDatabaseInfoInternalAsync(mod.ModId, mod.Version, normalizedGameVersion, cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                mod.DatabaseInfo = info;
            }
        }
    }

    public Task<ModDatabaseInfo?> TryLoadDatabaseInfoAsync(string modId, string? modVersion, string? installedGameVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return Task.FromResult<ModDatabaseInfo?>(null);
        }

        string? normalizedGameVersion = VersionStringUtility.Normalize(installedGameVersion);
        return TryLoadDatabaseInfoInternalAsync(modId, modVersion, normalizedGameVersion, cancellationToken);
    }

    public async Task<IReadOnlyList<ModDatabaseSearchResult>> SearchModsAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return Array.Empty<ModDatabaseSearchResult>();
        }

        string trimmed = query.Trim();
        IReadOnlyList<string> tokens = CreateSearchTokens(trimmed);
        if (tokens.Count == 0)
        {
            return Array.Empty<ModDatabaseSearchResult>();
        }

        int requestLimit = Math.Clamp(maxResults * 4, maxResults, 100);
        string requestUri = string.Format(
            CultureInfo.InvariantCulture,
            SearchEndpointFormat,
            Uri.EscapeDataString(trimmed),
            requestLimit.ToString(CultureInfo.InvariantCulture));

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
            using HttpResponseMessage response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<ModDatabaseSearchResult>();
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("mods", out JsonElement modsElement)
                || modsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ModDatabaseSearchResult>();
            }

            var candidates = new List<ModDatabaseSearchResult>();

            foreach (JsonElement modElement in modsElement.EnumerateArray())
            {
                if (modElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                ModDatabaseSearchResult? result = TryCreateSearchResult(modElement, tokens);
                if (result != null)
                {
                    candidates.Add(result);
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Downloads)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Array.Empty<ModDatabaseSearchResult>();
        }
    }

    private static async Task<ModDatabaseInfo?> TryLoadDatabaseInfoInternalAsync(string modId, string? modVersion, string? normalizedGameVersion, CancellationToken cancellationToken)
    {
        try
        {
            string requestUri = string.Format(CultureInfo.InvariantCulture, ApiEndpointFormat, Uri.EscapeDataString(modId));
            using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("mod", out JsonElement modElement) || modElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var tags = GetStringList(modElement, "tags");
            string? assetId = TryGetAssetId(modElement);
            string? modPageUrl = assetId == null ? null : ModPageBaseUrl + assetId;
            int? downloads = GetNullableInt(modElement, "downloads");
            int? comments = GetNullableInt(modElement, "comments");
            int? follows = GetNullableInt(modElement, "follows");
            int? trendingPoints = GetNullableInt(modElement, "trendingpoints");
            string? logoUrl = GetString(modElement, "logofile");
            IReadOnlyList<ModReleaseInfo> releases = BuildReleaseInfos(modElement, normalizedGameVersion);
            ModReleaseInfo? latestRelease = releases.Count > 0 ? releases[0] : null;
            ModReleaseInfo? latestCompatibleRelease = releases.FirstOrDefault(release => release.IsCompatibleWithInstalledGame);
            string? latestVersion = latestRelease?.Version;
            string? latestCompatibleVersion = latestCompatibleRelease?.Version;
            IReadOnlyList<string> requiredVersions = FindRequiredGameVersions(modElement, modVersion);

            return new ModDatabaseInfo
            {
                Tags = tags,
                AssetId = assetId,
                ModPageUrl = modPageUrl,
                LatestCompatibleVersion = latestCompatibleVersion,
                LatestVersion = latestVersion,
                RequiredGameVersions = requiredVersions,
                Downloads = downloads,
                Comments = comments,
                Follows = follows,
                TrendingPoints = trendingPoints,
                LogoUrl = logoUrl,
                LatestRelease = latestRelease,
                LatestCompatibleRelease = latestCompatibleRelease,
                Releases = releases
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> GetStringList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text);
                }
            }
        }

        return list.Count == 0 ? Array.Empty<string>() : list.ToArray();
    }

    private static string? TryGetAssetId(JsonElement element)
    {
        if (!element.TryGetProperty("assetid", out JsonElement assetIdElement))
        {
            return null;
        }

        return assetIdElement.ValueKind switch
        {
            JsonValueKind.Number when assetIdElement.TryGetInt64(out long number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.Number when assetIdElement.TryGetDecimal(out decimal decimalValue) => decimalValue.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => string.IsNullOrWhiteSpace(assetIdElement.GetString()) ? null : assetIdElement.GetString(),
            _ => null
        };
    }

    private static IReadOnlyList<ModReleaseInfo> BuildReleaseInfos(JsonElement modElement, string? normalizedGameVersion)
    {
        if (!modElement.TryGetProperty("releases", out JsonElement releasesElement) || releasesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ModReleaseInfo>();
        }

        var releases = new List<ModReleaseInfo>();

        foreach (JsonElement releaseElement in releasesElement.EnumerateArray())
        {
            if (releaseElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryCreateReleaseInfo(releaseElement, normalizedGameVersion, out ModReleaseInfo release))
            {
                releases.Add(release);
            }
        }

        return releases.Count == 0 ? Array.Empty<ModReleaseInfo>() : releases;
    }

    private static bool TryCreateReleaseInfo(JsonElement releaseElement, string? normalizedGameVersion, out ModReleaseInfo release)
    {
        release = default!;

        string? downloadUrl = GetString(releaseElement, "mainfile");
        if (string.IsNullOrWhiteSpace(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri? downloadUri))
        {
            return false;
        }

        string? version = ExtractReleaseVersion(releaseElement);
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        string? normalizedVersion = VersionStringUtility.Normalize(version);
        IReadOnlyList<string> releaseTags = GetStringList(releaseElement, "tags");
        bool isCompatible = false;

        if (normalizedGameVersion != null && releaseTags.Count > 0)
        {
            foreach (string tag in releaseTags)
            {
                string? normalizedTag = VersionStringUtility.Normalize(tag);
                if (normalizedTag != null && string.Equals(normalizedTag, normalizedGameVersion, StringComparison.OrdinalIgnoreCase))
                {
                    isCompatible = true;
                    break;
                }
            }
        }

        string? fileName = GetString(releaseElement, "filename");
        string? changelog = ConvertChangelogToPlainText(GetString(releaseElement, "changelog"));

        release = new ModReleaseInfo
        {
            Version = version!,
            NormalizedVersion = normalizedVersion,
            DownloadUri = downloadUri,
            FileName = fileName,
            GameVersionTags = releaseTags,
            IsCompatibleWithInstalledGame = isCompatible,
            Changelog = changelog
        };

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ConvertChangelogToPlainText(string? changelog)
    {
        if (string.IsNullOrWhiteSpace(changelog))
        {
            return null;
        }

        string text = changelog.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        text = text.Replace('\r', '\n');
        text = HtmlBreakRegex.Replace(text, "\n");
        text = HtmlParagraphCloseRegex.Replace(text, "\n\n");
        text = HtmlParagraphOpenRegex.Replace(text, string.Empty);
        text = HtmlListItemCloseRegex.Replace(text, "\n");
        text = HtmlListItemOpenRegex.Replace(text, "\u2022 ");
        text = HtmlBlockCloseRegex.Replace(text, "\n\n");
        text = HtmlTagRegex.Replace(text, string.Empty);

        text = WebUtility.HtmlDecode(text);

        string[] lines = text.Split('\n');
        var normalizedLines = new List<string>(lines.Length);

        foreach (string line in lines)
        {
            string trimmedEnd = line.TrimEnd();
            if (trimmedEnd.Length == 0)
            {
                if (normalizedLines.Count == 0 || normalizedLines[^1].Length == 0)
                {
                    continue;
                }

                normalizedLines.Add(string.Empty);
                continue;
            }

            string trimmedStart = trimmedEnd.TrimStart();
            if (trimmedStart.StartsWith("\u2022 ", StringComparison.Ordinal))
            {
                trimmedStart = "\u2022 " + trimmedStart[2..].Trim();
            }
            else
            {
                trimmedStart = trimmedStart.Trim();
            }

            normalizedLines.Add(trimmedStart);
        }

        while (normalizedLines.Count > 0 && normalizedLines[^1].Length == 0)
        {
            normalizedLines.RemoveAt(normalizedLines.Count - 1);
        }

        if (normalizedLines.Count == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, normalizedLines);
    }

    private static ModDatabaseSearchResult? TryCreateSearchResult(JsonElement element, IReadOnlyList<string> tokens)
    {
        string? name = GetString(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        IReadOnlyList<string> modIds = GetStringList(element, "modidstrs");
        string? primaryId = modIds.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
            ?? GetString(element, "urlalias")
            ?? name;

        if (string.IsNullOrWhiteSpace(primaryId))
        {
            return null;
        }

        primaryId = primaryId.Trim();

        string? summary = GetString(element, "summary");
        string? author = GetString(element, "author");
        string? assetId = TryGetAssetId(element);
        string? urlAlias = GetString(element, "urlalias");
        string? side = GetString(element, "side");
        string? logo = GetString(element, "logofile");

        IReadOnlyList<string> tags = GetStringList(element, "tags");
        int downloads = GetInt(element, "downloads");
        int follows = GetInt(element, "follows");
        int trendingPoints = GetInt(element, "trendingpoints");
        int comments = GetInt(element, "comments");
        DateTime? lastReleased = TryParseDateTime(GetString(element, "lastreleased"));

        IReadOnlyList<string> alternateIds = modIds.Count == 0 ? new[] { primaryId } : modIds;

        if (!TryCalculateSearchScore(
                name,
                primaryId,
                summary,
                alternateIds,
                tags,
                tokens,
                downloads,
                follows,
                trendingPoints,
                comments,
                lastReleased,
                out double score))
        {
            return null;
        }

        return new ModDatabaseSearchResult
        {
            Name = name,
            ModId = primaryId,
            AlternateIds = alternateIds,
            Summary = summary,
            Author = author,
            Tags = tags,
            Downloads = downloads,
            Follows = follows,
            TrendingPoints = trendingPoints,
            Comments = comments,
            AssetId = assetId,
            UrlAlias = urlAlias,
            Side = side,
            LogoUrl = logo,
            LastReleasedUtc = lastReleased,
            Score = score
        };
    }

    private static bool TryCalculateSearchScore(
        string name,
        string primaryId,
        string? summary,
        IReadOnlyList<string> alternateIds,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> tokens,
        int downloads,
        int follows,
        int trendingPoints,
        int comments,
        DateTime? lastReleased,
        out double score)
    {
        score = 0;

        if (tokens.Count == 0)
        {
            return false;
        }

        int matchedTokenCount = 0;
        string summaryText = summary ?? string.Empty;

        foreach (string token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            string currentToken = token.Trim();
            if (currentToken.Length == 0)
            {
                continue;
            }

            bool tokenMatched = false;

            bool nameExactMatch = string.Equals(name, currentToken, StringComparison.OrdinalIgnoreCase);
            if (nameExactMatch)
            {
                score += 12;
                tokenMatched = true;
            }
            else if (name.Contains(currentToken, StringComparison.OrdinalIgnoreCase))
            {
                score += 6;
                tokenMatched = true;
            }

            bool primaryExactMatch = string.Equals(primaryId, currentToken, StringComparison.OrdinalIgnoreCase);
            if (primaryExactMatch)
            {
                score += 10;
                tokenMatched = true;
            }
            else if (primaryId.Contains(currentToken, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
                tokenMatched = true;
            }
            else
            {
                bool alternateExact = alternateIds.Any(id => string.Equals(id, currentToken, StringComparison.OrdinalIgnoreCase));
                if (alternateExact)
                {
                    score += 9;
                    tokenMatched = true;
                }
                else if (alternateIds.Any(id => id.Contains(currentToken, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 4;
                    tokenMatched = true;
                }
            }

            bool tagExactMatch = tags.Any(tag => string.Equals(tag, currentToken, StringComparison.OrdinalIgnoreCase));
            if (tagExactMatch)
            {
                score += 3;
                tokenMatched = true;
            }
            else if (tags.Any(tag => tag.Contains(currentToken, StringComparison.OrdinalIgnoreCase)))
            {
                score += 2;
                tokenMatched = true;
            }

            if (!string.IsNullOrEmpty(summaryText)
                && summaryText.Contains(currentToken, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.5;
                tokenMatched = true;
            }

            if (tokenMatched)
            {
                matchedTokenCount++;
            }
        }

        if (matchedTokenCount == 0)
        {
            score = 0;
            return false;
        }

        score += matchedTokenCount * 1.5;
        score += Math.Log10(downloads + 1) * 1.2;
        score += Math.Log10(follows + 1) * 1.5;
        score += Math.Log10(trendingPoints + 1);
        score += Math.Log10(comments + 1) * 0.5;

        if (lastReleased.HasValue)
        {
            double days = (DateTime.UtcNow - lastReleased.Value).TotalDays;
            if (!double.IsNaN(days))
            {
                score += Math.Max(0, 4 - (days / 45.0));
            }
        }

        return true;
    }

    private static IReadOnlyList<string> CreateSearchTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        string trimmed = value.Trim();
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            token = token.Trim();
            if (seen.Add(token))
            {
                tokens.Add(token);
            }
        }

        AddToken(trimmed);

        foreach (string token in trimmed.Split(
                     [' ', '\t', '\r', '\n', '-', '_', '.', '/', '\\'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddToken(token);
        }

        return tokens.Count == 0 ? Array.Empty<string>() : tokens;
    }

    private static int? GetNullableInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetInt64(out long longValue):
                return (int)Math.Clamp(longValue, int.MinValue, int.MaxValue);
            case JsonValueKind.Number when value.TryGetDouble(out double doubleValue):
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    return null;
                }

                double truncated = Math.Truncate(doubleValue);
                if (truncated < int.MinValue)
                {
                    return int.MinValue;
                }

                if (truncated > int.MaxValue)
                {
                    return int.MaxValue;
                }

                return (int)truncated;
            case JsonValueKind.String when long.TryParse(
                    value.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long parsed):
                return (int)Math.Clamp(parsed, int.MinValue, int.MaxValue);
            default:
                return null;
        }
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return GetNullableInt(element, propertyName) ?? 0;
    }

    private static DateTime? TryParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime result))
        {
            return result;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }

    private static IReadOnlyList<string> FindRequiredGameVersions(JsonElement modElement, string? modVersion)
    {
        if (string.IsNullOrWhiteSpace(modVersion))
        {
            return Array.Empty<string>();
        }

        if (!modElement.TryGetProperty("releases", out JsonElement releasesElement) || releasesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        string? normalizedModVersion = VersionStringUtility.Normalize(modVersion);

        foreach (JsonElement release in releasesElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!release.TryGetProperty("modversion", out JsonElement releaseModVersionElement) || releaseModVersionElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string? releaseModVersion = releaseModVersionElement.GetString();
            if (string.IsNullOrWhiteSpace(releaseModVersion))
            {
                continue;
            }

            if (!ReleaseMatchesModVersion(releaseModVersion, modVersion, normalizedModVersion))
            {
                continue;
            }

            var tags = GetStringList(release, "tags");
            return tags.Count == 0 ? Array.Empty<string>() : tags;
        }

        return Array.Empty<string>();
    }

    private static bool ReleaseMatchesModVersion(string releaseModVersion, string? modVersion, string? normalizedModVersion)
    {
        if (modVersion != null && string.Equals(releaseModVersion, modVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string? normalizedReleaseVersion = VersionStringUtility.Normalize(releaseModVersion);
        if (normalizedReleaseVersion == null || normalizedModVersion == null)
        {
            return false;
        }

        return string.Equals(normalizedReleaseVersion, normalizedModVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractReleaseVersion(JsonElement releaseElement)
    {
        if (releaseElement.TryGetProperty("modversion", out JsonElement modVersion) && modVersion.ValueKind == JsonValueKind.String)
        {
            string? value = modVersion.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (releaseElement.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.String)
        {
            string? value = version.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
