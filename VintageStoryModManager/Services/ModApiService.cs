using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Service for interacting with the Vintage Story mod database API.
/// </summary>
public interface IModApiService
{
    /// <summary>
    /// Queries the mod database with optional filters.
    /// </summary>
    Task<List<DownloadableModOnList>> QueryModsAsync(
        string? textFilter = null,
        ModAuthor? authorFilter = null,
        IEnumerable<GameVersion>? versionsFilter = null,
        IEnumerable<ModTag>? tagsFilter = null,
        string orderBy = "follows",
        string orderByOrder = "desc",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single mod by its ID.
    /// </summary>
    Task<DownloadableMod?> GetModAsync(int modId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single mod by its string ID.
    /// </summary>
    Task<DownloadableMod?> GetModAsync(string modIdStr, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available authors.
    /// </summary>
    Task<List<ModAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available game versions.
    /// </summary>
    Task<List<GameVersion>> GetGameVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available tags.
    /// </summary>
    Task<List<ModTag>> GetTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a mod file to the specified path.
    /// </summary>
    Task<bool> DownloadModAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the mod API service.
/// </summary>
public class ModApiService : IModApiService
{
    private const string BaseUrl = "https://mods.vintagestory.at";
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ModApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<DownloadableModOnList>> QueryModsAsync(
        string? textFilter = null,
        ModAuthor? authorFilter = null,
        IEnumerable<GameVersion>? versionsFilter = null,
        IEnumerable<ModTag>? tagsFilter = null,
        string orderBy = "follows",
        string orderByOrder = "desc",
        CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping mod query");
            return [];
        }

        try
        {
            var queryBuilder = new StringBuilder($"{BaseUrl}/api/mods?");
            var parameters = new List<string>();

            if (!string.IsNullOrWhiteSpace(textFilter) && textFilter.Length > 1)
            {
                parameters.Add($"text={Uri.EscapeDataString(textFilter)}");
            }

            if (authorFilter != null && !string.IsNullOrEmpty(authorFilter.UserId))
            {
                parameters.Add($"author={Uri.EscapeDataString(authorFilter.UserId)}");
            }

            if (versionsFilter != null)
            {
                foreach (var version in versionsFilter)
                {
                    parameters.Add($"gameversions[]={Uri.EscapeDataString(version.TagId)}");
                }
            }

            if (tagsFilter != null)
            {
                foreach (var tag in tagsFilter)
                {
                    parameters.Add($"tagids[]={tag.TagId}");
                }
            }

            parameters.Add($"orderby={Uri.EscapeDataString(orderBy)}");
            parameters.Add($"orderdirection={Uri.EscapeDataString(orderByOrder)}");

            queryBuilder.Append(string.Join("&", parameters));

            var response = await _httpClient.GetStringAsync(queryBuilder.ToString(), cancellationToken);
            var result = JsonSerializer.Deserialize<ModListResponse>(response, _jsonOptions);

            return result?.Mods ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching mods: {ex.Message}");
            return [];
        }
    }

    public async Task<DownloadableMod?> GetModAsync(int modId, CancellationToken cancellationToken = default)
    {
        return await GetModAsync(modId.ToString(), cancellationToken);
    }

    public async Task<DownloadableMod?> GetModAsync(string modIdStr, CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping mod fetch");
            return null;
        }

        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/mod/{Uri.EscapeDataString(modIdStr)}", cancellationToken);
            var result = JsonSerializer.Deserialize<ModResponse>(response, _jsonOptions);

            if (result?.StatusCode != 200)
            {
                return null;
            }

            return result.Mod;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching mod {modIdStr}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ModAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping authors fetch");
            return [];
        }

        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/authors", cancellationToken);
            var result = JsonSerializer.Deserialize<AuthorsResponse>(response, _jsonOptions);

            return result?.Authors ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching authors: {ex.Message}");
            return [];
        }
    }

    public async Task<List<GameVersion>> GetGameVersionsAsync(CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping game versions fetch");
            return [];
        }

        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/gameversions", cancellationToken);
            var result = JsonSerializer.Deserialize<GameVersionsResponse>(response, _jsonOptions);

            var versions = result?.GameVersions ?? [];
            // Return in reverse order (most recent first) without modifying original
            return versions.AsEnumerable().Reverse().ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching game versions: {ex.Message}");
            return [];
        }
    }

    public async Task<List<ModTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping tags fetch");
            return [];
        }

        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/tags", cancellationToken);
            var result = JsonSerializer.Deserialize<TagsResponse>(response, _jsonOptions);

            return result?.Tags ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching tags: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> DownloadModAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping mod download");
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)totalBytesRead / totalBytes * 100);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading mod: {ex.Message}");
            return false;
        }
    }
}
