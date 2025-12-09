using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        bool includeLatestRelease = false,
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
    /// Gets the latest release for a mod.
    /// </summary>
    Task<DownloadableModRelease?> GetLatestReleaseAsync(int modId, CancellationToken cancellationToken = default);

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
        bool includeLatestRelease = false,
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
                    parameters.Add($"gameversions[]={Uri.EscapeDataString(version.TagId.ToString())}");
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

            var mods = result?.Mods ?? [];

            if (includeLatestRelease && mods.Count > 0)
            {
                await PopulateLatestReleaseInfoAsync(mods, cancellationToken);
            }

            return mods;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching mods: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return [];
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching mods: {ex.Message}");
            return [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching mods: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Exception details: {ex}");
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
            var url = $"{BaseUrl}/api/mod/{Uri.EscapeDataString(modIdStr)}";
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Fetching mod from: {url}");
            
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            
            // Log response metadata instead of full content for performance
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Received response: {response.Length} characters");
            
            var result = JsonSerializer.Deserialize<ModResponse>(response, _jsonOptions);

            if (result?.StatusCode != 200)
            {
                System.Diagnostics.Debug.WriteLine($"[ModApiService] Non-200 status code: {result?.StatusCode}");
                return null;
            }

            var mod = result.Mod;
            if (mod != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ModApiService] Successfully deserialized mod: {mod.Name}");
                System.Diagnostics.Debug.WriteLine($"[ModApiService] Mod has {mod.Releases?.Count ?? 0} releases");
                
                if (mod.Releases == null || mod.Releases.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModApiService] WARNING: Mod {mod.Name} has NO releases!");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ModApiService] Deserialized mod is null");
            }
            
            return mod;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching mod {modIdStr}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching mod {modIdStr}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching mod {modIdStr}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Exception details: {ex}");
            return null;
        }
    }

    public async Task<DownloadableModRelease?> GetLatestReleaseAsync(int modId, CancellationToken cancellationToken = default)
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Internet access is disabled - skipping latest release fetch");
            return null;
        }

        try
        {
            var url = $"{BaseUrl}/api/mod/{Uri.EscapeDataString(modId.ToString())}/latestrelease";
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Fetching latest release from: {url}");

            var response = await _httpClient.GetStringAsync(url, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[ModApiService] Latest release response length: {response.Length} characters");

            var result = JsonSerializer.Deserialize<ModResponse>(response, _jsonOptions);

            if (result?.StatusCode != 200)
            {
                System.Diagnostics.Debug.WriteLine($"[ModApiService] Non-200 status code for latest release: {result?.StatusCode}");
                return null;
            }

            var latestRelease = result.Mod?.Releases?
                .OrderByDescending(r => DateTime.TryParse(r.Created, out var date) ? date : DateTime.MinValue)
                .FirstOrDefault();

            if (latestRelease != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ModApiService] Latest release for mod {modId}: {latestRelease.ModVersion}");
            }

            return latestRelease;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching latest release for mod {modId}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching latest release for mod {modId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching latest release for mod {modId}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Exception details: {ex}");
            return null;
        }
    }

    private async Task PopulateLatestReleaseInfoAsync(IEnumerable<DownloadableModOnList> mods, CancellationToken cancellationToken)
    {
        const int maxConcurrent = 5;
        using var semaphore = new SemaphoreSlim(maxConcurrent);

        var tasks = mods.Select(async mod =>
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);

                var latestRelease = await GetLatestReleaseAsync(mod.ModId, cancellationToken);
                if (latestRelease != null)
                {
                    mod.LatestReleaseVersion = latestRelease.ModVersion;
                    mod.LatestReleaseTags = latestRelease.Tags?.ToList() ?? [];
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
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
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching authors: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return [];
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching authors: {ex.Message}");
            return [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching authors: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Exception details: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"Fetching game versions from {BaseUrl}/api/gameversions");
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/gameversions", cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Raw API response (first 500 chars): {(response.Length > 500 ? response.Substring(0, 500) : response)}");
            
            var result = JsonSerializer.Deserialize<GameVersionsResponse>(response, _jsonOptions);
            System.Diagnostics.Debug.WriteLine($"Deserialized {result?.GameVersions?.Count ?? 0} game versions");

            var versions = result?.GameVersions ?? [];
            // Return in reverse order (most recent first) without modifying original
            var reversedVersions = versions.AsEnumerable().Reverse().ToList();
            System.Diagnostics.Debug.WriteLine($"Returning {reversedVersions.Count} game versions");
            return reversedVersions;
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching game versions: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return [];
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching game versions: {ex.Message}");
            return [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching game versions: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Stack trace: {ex.StackTrace}");
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
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON deserialization error fetching tags: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] JSON exception details: {ex}");
            return [];
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] HTTP request error fetching tags: {ex.Message}");
            return [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Unexpected error fetching tags: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ModApiService] Exception details: {ex}");
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
