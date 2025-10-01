using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
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
    private const string ModPageBaseUrl = "https://mods.vintagestory.at/show/mod/";

    private static readonly HttpClient HttpClient = new();

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

            ModDatabaseInfo? info = await TryLoadDatabaseInfoAsync(mod.ModId, mod.Version, normalizedGameVersion, cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                mod.DatabaseInfo = info;
            }
        }
    }

    private static async Task<ModDatabaseInfo?> TryLoadDatabaseInfoAsync(string modId, string? modVersion, string? normalizedGameVersion, CancellationToken cancellationToken)
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
            string? latestVersion = FindLatestReleaseVersion(modElement);
            string? latestCompatibleVersion = FindCompatibleReleaseVersion(modElement, normalizedGameVersion);
            IReadOnlyList<string> requiredVersions = FindRequiredGameVersions(modElement, modVersion);

            return new ModDatabaseInfo
            {
                Tags = tags,
                AssetId = assetId,
                ModPageUrl = modPageUrl,
                LatestCompatibleVersion = latestCompatibleVersion,
                LatestVersion = latestVersion,
                RequiredGameVersions = requiredVersions
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

    private static string? FindLatestReleaseVersion(JsonElement modElement)
    {
        if (!modElement.TryGetProperty("releases", out JsonElement releasesElement) || releasesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement release in releasesElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? version = ExtractReleaseVersion(release);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }

    private static string? FindCompatibleReleaseVersion(JsonElement modElement, string? normalizedGameVersion)
    {
        if (normalizedGameVersion == null)
        {
            return null;
        }

        if (!modElement.TryGetProperty("releases", out JsonElement releasesElement) || releasesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement release in releasesElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var tags = GetStringList(release, "tags");
            foreach (string tag in tags)
            {
                string? normalizedTag = VersionStringUtility.Normalize(tag);
                if (normalizedTag != null && string.Equals(normalizedTag, normalizedGameVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractReleaseVersion(release);
                }
            }
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
