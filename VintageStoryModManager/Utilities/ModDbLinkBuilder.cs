using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VintageStoryModManager.Utilities;

/// <summary>
/// Provides helpers for constructing links to the official Vintage Story Mod DB.
/// </summary>
internal static class ModDbLinkBuilder
{
    private const string BaseUrl = "https://mods.vintagestory.at/";
    private const string ApiUrl = "https://mods.vintagestory.at/api/mods";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, Uri> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Uri? TryCreateEntryUri(string? modId)
    {
        string slug = CreateSlug(modId);
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }

        if (!Uri.TryCreate(BaseUrl + slug, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        return uri;
    }

    public static async Task<Uri?> TryFetchEntryUriAsync(string? modId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        if (Cache.TryGetValue(modId, out Uri? cached))
        {
            return cached;
        }

        try
        {
            string requestUri = $"{ApiUrl}?search={Uri.EscapeDataString(modId)}";
            await using var stream = await HttpClient.GetStreamAsync(requestUri, cancellationToken).ConfigureAwait(false);
            ModDbSearchResponse? response = await JsonSerializer.DeserializeAsync<ModDbSearchResponse>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            string? alias = TrySelectAlias(response, modId);
            if (!string.IsNullOrWhiteSpace(alias) && Uri.TryCreate(BaseUrl + alias, UriKind.Absolute, out Uri? uri))
            {
                Cache[modId] = uri;
                return uri;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignore network and parsing errors and fall back to slug generation.
        }

        return null;
    }

    private static string CreateSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch == '-' || ch == '_')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        string slug = builder.ToString().Trim('-');
        if (slug.Length > 0)
        {
            return slug;
        }

        builder.Clear();
        foreach (char ch in value)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                if (builder.Length == 0)
                {
                    builder.Append('m');
                }

                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VintageStoryModManager/1.0");
        return client;
    }

    private static string? TrySelectAlias(ModDbSearchResponse? response, string modId)
    {
        if (response?.Mods == null)
        {
            return null;
        }

        ModDbEntry? exact = response.Mods.FirstOrDefault(entry => entry.ModIdStrs?.Any(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase)) == true);
        if (!string.IsNullOrWhiteSpace(exact?.UrlAlias))
        {
            return exact.UrlAlias;
        }

        return response.Mods.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.UrlAlias))?.UrlAlias;
    }

    private sealed class ModDbSearchResponse
    {
        [JsonPropertyName("mods")]
        public ModDbEntry[]? Mods { get; set; }
    }

    private sealed class ModDbEntry
    {
        [JsonPropertyName("urlalias")]
        public string? UrlAlias { get; set; }

        [JsonPropertyName("modidstrs")]
        public string[]? ModIdStrs { get; set; }
    }
}
