using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides caching for mod database list queries (most downloaded, recently added, search results, etc.)
///     to avoid re-downloading identical payloads from the API.
/// </summary>
internal sealed class ModDatabaseQueryCacheService
{
    private const int CacheSchemaVersion = 1;
    private const string QueryCacheDirectoryName = "Query Cache";

    /// <summary>
    ///     Cache expiry time for query results. Results older than this will be refreshed.
    ///     Set to a reasonable duration that balances freshness with network efficiency.
    /// </summary>
    private static readonly TimeSpan QueryCacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Maximum number of query cache entries to keep in memory.
    /// </summary>
    private const int MaxInMemoryCacheSize = 50;

    /// <summary>
    ///     How long an in-memory cache entry is valid before requiring disk re-read.
    /// </summary>
    private static readonly TimeSpan InMemoryCacheMaxAge = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     In-memory cache to avoid repeated disk reads for the same query.
    /// </summary>
    private readonly ConcurrentDictionary<string, InMemoryCacheEntry> _inMemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Clears all cached query results.
    /// </summary>
    internal static void ClearCacheDirectory()
    {
        var baseDirectory = GetQueryCacheDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory)) return;

        try
        {
            Directory.Delete(baseDirectory, true);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete the mod database query cache at {baseDirectory}.", ex);
        }
    }

    /// <summary>
    ///     Clears the in-memory cache.
    /// </summary>
    internal void ClearInMemoryCache()
    {
        _inMemoryCache.Clear();
    }

    /// <summary>
    ///     Attempts to load cached query results.
    /// </summary>
    /// <param name="queryKey">A unique key identifying the query (endpoint, parameters, etc.).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached results and HTTP headers, or null if not cached or expired.</returns>
    public async Task<QueryCacheResult?> TryLoadAsync(
        string queryKey,
        CancellationToken cancellationToken)
    {
        var cachePath = GetCacheFilePath(queryKey);
        if (string.IsNullOrWhiteSpace(cachePath)) return null;

        // Try in-memory cache first
        if (_inMemoryCache.TryGetValue(cachePath, out var memoryEntry))
        {
            if (!IsInMemoryCacheEntryExpired(memoryEntry))
            {
                return memoryEntry.Result;
            }

            _inMemoryCache.TryRemove(cachePath, out _);
        }

        if (!File.Exists(cachePath)) return null;

        var fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(cachePath)) return null;

            await using FileStream stream = new(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var cached = await JsonSerializer
                .DeserializeAsync<CachedQueryResult>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (cached is null || cached.SchemaVersion != CacheSchemaVersion)
                return null;

            // Check if cache has expired
            if (DateTime.UtcNow - cached.CachedUtc > QueryCacheExpiry)
            {
                // Return the cached data but indicate it's stale (via expired flag)
                var staleResult = new QueryCacheResult(
                    cached.Results,
                    cached.LastModifiedHeader,
                    cached.ETag,
                    IsExpired: true);

                TryAddToInMemoryCache(cachePath, staleResult);
                return staleResult;
            }

            var result = new QueryCacheResult(
                cached.Results,
                cached.LastModifiedHeader,
                cached.ETag,
                IsExpired: false);

            TryAddToInMemoryCache(cachePath, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    ///     Gets cached HTTP headers for conditional request validation.
    /// </summary>
    /// <param name="queryKey">A unique key identifying the query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A tuple containing the Last-Modified header and ETag, or nulls if not cached.</returns>
    public async Task<(string? LastModified, string? ETag)> GetCachedHttpHeadersAsync(
        string queryKey,
        CancellationToken cancellationToken)
    {
        var cachePath = GetCacheFilePath(queryKey);
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath)) return (null, null);

        var fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(cachePath)) return (null, null);

            await using FileStream stream = new(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var cached = await JsonSerializer
                .DeserializeAsync<CachedQueryResult>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (cached is null || cached.SchemaVersion != CacheSchemaVersion) return (null, null);

            return (cached.LastModifiedHeader, cached.ETag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, null);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    ///     Stores query results to the cache.
    /// </summary>
    /// <param name="queryKey">A unique key identifying the query.</param>
    /// <param name="results">The query results to cache.</param>
    /// <param name="lastModifiedHeader">The Last-Modified header from the HTTP response.</param>
    /// <param name="etag">The ETag header from the HTTP response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task StoreAsync(
        string queryKey,
        IReadOnlyList<CachedSearchResult> results,
        string? lastModifiedHeader,
        string? etag,
        CancellationToken cancellationToken)
    {
        if (results is null) return;

        var cachePath = GetCacheFilePath(queryKey);
        if (string.IsNullOrWhiteSpace(cachePath)) return;

        var directory = Path.GetDirectoryName(cachePath);
        if (string.IsNullOrWhiteSpace(directory)) return;

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception)
        {
            return;
        }

        var fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            var tempPath = cachePath + ".tmp";

            var cacheModel = new CachedQueryResult
            {
                SchemaVersion = CacheSchemaVersion,
                CachedUtc = DateTime.UtcNow,
                QueryKey = queryKey,
                LastModifiedHeader = lastModifiedHeader,
                ETag = etag,
                Results = results.ToArray()
            };

            await using (FileStream tempStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(tempStream, cacheModel, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Use File.Replace for atomic updates when the target already exists,
            // otherwise use File.Move for new files
            try
            {
                if (File.Exists(cachePath))
                {
                    File.Replace(tempPath, cachePath, null);
                }
                else
                {
                    File.Move(tempPath, cachePath, true);
                }
            }
            catch (IOException)
            {
                // Fallback: try alternative approach if primary method fails
                try
                {
                    if (File.Exists(cachePath))
                        File.Replace(tempPath, cachePath, null);
                    else
                        File.Move(tempPath, cachePath, true);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }

            // Invalidate in-memory cache for this query
            _inMemoryCache.TryRemove(cachePath, out _);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Ignore serialization or file system failures to keep cache best-effort.
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    ///     Generates a unique cache key for a query based on its parameters.
    /// </summary>
    /// <param name="queryType">The type of query (search, mostDownloaded, recentlyAdded, etc.).</param>
    /// <param name="parameters">Additional parameters that affect the query results.</param>
    /// <returns>A cache key string.</returns>
    public static string BuildQueryKey(string queryType, params string?[] parameters)
    {
        var parts = new List<string> { queryType };
        foreach (var param in parameters)
        {
            if (!string.IsNullOrWhiteSpace(param))
                parts.Add(param);
        }

        return string.Join("_", parts);
    }

    private static string? GetQueryCacheDirectory()
    {
        var managerDirectory = ModCacheLocator.GetManagerDataDirectory();
        return managerDirectory is null
            ? null
            : Path.Combine(managerDirectory, "Mod Database Cache", QueryCacheDirectoryName);
    }

    private static string? GetCacheFilePath(string queryKey)
    {
        if (string.IsNullOrWhiteSpace(queryKey)) return null;

        var baseDirectory = GetQueryCacheDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        var safeKey = ModCacheLocator.SanitizeFileName(queryKey, "query");
        return Path.Combine(baseDirectory, $"{safeKey}.json");
    }

    private async Task<SemaphoreSlim> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        var gate = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return gate;
    }

    private static bool IsInMemoryCacheEntryExpired(InMemoryCacheEntry entry)
    {
        return DateTime.UtcNow - entry.CreatedUtc > InMemoryCacheMaxAge;
    }

    private void TryAddToInMemoryCache(string key, QueryCacheResult? result)
    {
        if (_inMemoryCache.Count >= MaxInMemoryCacheSize)
        {
            EvictOldestCacheEntries();
        }

        var entry = new InMemoryCacheEntry
        {
            Result = result,
            CreatedUtc = DateTime.UtcNow
        };

        _inMemoryCache.TryAdd(key, entry);
    }

    private void EvictOldestCacheEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();
        DateTime? oldestTimestamp = null;
        string? oldestKey = null;

        foreach (var kvp in _inMemoryCache)
        {
            if (now - kvp.Value.CreatedUtc > InMemoryCacheMaxAge)
            {
                expiredKeys.Add(kvp.Key);
            }
            else if (oldestTimestamp is null || kvp.Value.CreatedUtc < oldestTimestamp)
            {
                oldestTimestamp = kvp.Value.CreatedUtc;
                oldestKey = kvp.Key;
            }
        }

        foreach (var key in expiredKeys)
        {
            _inMemoryCache.TryRemove(key, out _);
        }

        if (_inMemoryCache.Count >= MaxInMemoryCacheSize && oldestKey != null)
        {
            _inMemoryCache.TryRemove(oldestKey, out _);
        }
    }

    /// <summary>
    ///     Represents a cached query result loaded from disk or memory.
    /// </summary>
    internal sealed record QueryCacheResult(
        CachedSearchResult[] Results,
        string? LastModifiedHeader,
        string? ETag,
        bool IsExpired);

    /// <summary>
    ///     Represents the on-disk format for cached query results.
    /// </summary>
    private sealed class CachedQueryResult
    {
        public int SchemaVersion { get; init; }
        public DateTime CachedUtc { get; init; }
        public string QueryKey { get; init; } = string.Empty;
        public string? LastModifiedHeader { get; init; }
        public string? ETag { get; init; }
        public CachedSearchResult[] Results { get; init; } = Array.Empty<CachedSearchResult>();
    }

    /// <summary>
    ///     Represents an entry in the in-memory cache.
    /// </summary>
    private sealed class InMemoryCacheEntry
    {
        public required QueryCacheResult? Result { get; init; }
        public required DateTime CreatedUtc { get; init; }
    }
}

/// <summary>
///     Represents a single search result that can be serialized to the query cache.
/// </summary>
internal sealed class CachedSearchResult
{
    public required string ModId { get; init; }
    public required string Name { get; init; }
    public string[]? AlternateIds { get; init; }
    public string? Summary { get; init; }
    public string? Author { get; init; }
    public string[]? Tags { get; init; }
    public int Downloads { get; init; }
    public int Follows { get; init; }
    public int TrendingPoints { get; init; }
    public int Comments { get; init; }
    public string? AssetId { get; init; }
    public string? UrlAlias { get; init; }
    public string? Side { get; init; }
    public string? LogoUrl { get; init; }
    public DateTime? LastReleasedUtc { get; init; }
    public DateTime? CreatedUtc { get; init; }
    public double Score { get; init; }
    public int? LatestReleaseDownloads { get; init; }

    /// <summary>
    ///     Converts a ModDatabaseSearchResult to a CachedSearchResult for storage.
    /// </summary>
    public static CachedSearchResult FromSearchResult(ModDatabaseSearchResult result)
    {
        return new CachedSearchResult
        {
            ModId = result.ModId,
            Name = result.Name,
            AlternateIds = result.AlternateIds?.ToArray(),
            Summary = result.Summary,
            Author = result.Author,
            Tags = result.Tags?.ToArray(),
            Downloads = result.Downloads,
            Follows = result.Follows,
            TrendingPoints = result.TrendingPoints,
            Comments = result.Comments,
            AssetId = result.AssetId,
            UrlAlias = result.UrlAlias,
            Side = result.Side,
            LogoUrl = result.LogoUrl,
            LastReleasedUtc = result.LastReleasedUtc,
            CreatedUtc = result.CreatedUtc,
            Score = result.Score,
            LatestReleaseDownloads = result.LatestReleaseDownloads
        };
    }

    /// <summary>
    ///     Converts this cached result back to a ModDatabaseSearchResult.
    /// </summary>
    public ModDatabaseSearchResult ToSearchResult()
    {
        return new ModDatabaseSearchResult
        {
            ModId = ModId,
            Name = Name,
            AlternateIds = AlternateIds ?? Array.Empty<string>(),
            Summary = Summary,
            Author = Author,
            Tags = Tags ?? Array.Empty<string>(),
            Downloads = Downloads,
            Follows = Follows,
            TrendingPoints = TrendingPoints,
            Comments = Comments,
            AssetId = AssetId,
            UrlAlias = UrlAlias,
            Side = Side,
            LogoUrl = LogoUrl,
            LastReleasedUtc = LastReleasedUtc,
            CreatedUtc = CreatedUtc,
            Score = Score,
            LatestReleaseDownloads = LatestReleaseDownloads
        };
    }
}
