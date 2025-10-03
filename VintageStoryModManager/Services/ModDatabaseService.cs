using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                LatestRelease = latestRelease,
                LatestCompatibleRelease = latestCompatibleRelease
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

        release = new ModReleaseInfo
        {
            Version = version!,
            NormalizedVersion = normalizedVersion,
            DownloadUri = downloadUri,
            FileName = fileName,
            GameVersionTags = releaseTags,
            IsCompatibleWithInstalledGame = isCompatible
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
