using System.Collections.Concurrent;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides in-memory caching for mod database search results to reduce API calls
///     during browsing sessions. This is separate from the per-mod metadata cache and
///     focuses on caching list queries.
/// </summary>
internal sealed class ModDatabaseSearchCacheService
{
    /// <summary>
    ///     Cache expiry time for search results. Matches VS Launcher's recommended cache duration.
    /// </summary>
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Maximum number of cached search queries to keep in memory.
    /// </summary>
    private const int MaxCacheSize = 50;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Attempts to retrieve cached search results for a given query.
    /// </summary>
    /// <param name="cacheKey">The unique cache key for this search query.</param>
    /// <returns>The cached results, or null if not found or expired.</returns>
    public IReadOnlyList<ModDatabaseSearchResult>? TryGetCachedResults(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return null;

        if (!_cache.TryGetValue(cacheKey, out var entry)) return null;

        // Check if entry has expired
        if (DateTimeOffset.Now - entry.CachedAt > CacheExpiry)
        {
            // Remove expired entry
            _cache.TryRemove(cacheKey, out _);
            return null;
        }

        return entry.Results;
    }

    /// <summary>
    ///     Stores search results in the cache.
    /// </summary>
    /// <param name="cacheKey">The unique cache key for this search query.</param>
    /// <param name="results">The search results to cache.</param>
    public void StoreResults(string cacheKey, IReadOnlyList<ModDatabaseSearchResult> results)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return;
        if (results is null) return;

        // Enforce cache size limit by evicting oldest entries
        if (_cache.Count >= MaxCacheSize)
        {
            EvictOldestEntries();
        }

        var entry = new CacheEntry
        {
            Results = results,
            CachedAt = DateTimeOffset.Now
        };

        _cache.AddOrUpdate(cacheKey, entry, (_, _) => entry);
    }

    /// <summary>
    ///     Clears all cached search results.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    ///     Generates a cache key for a search query with filters.
    /// </summary>
    /// <param name="searchType">The type of search (e.g., "search", "trending", "recent").</param>
    /// <param name="query">The search query string (if applicable).</param>
    /// <param name="maxResults">The maximum number of results requested.</param>
    /// <returns>A unique cache key for this search configuration.</returns>
    public static string GenerateCacheKey(
        string searchType,
        string? query,
        int maxResults)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? "" : query.Trim().ToLowerInvariant();
        return $"{searchType}|{normalizedQuery}|{maxResults}";
    }

    /// <summary>
    ///     Evicts the oldest cache entries when the cache is full.
    /// </summary>
    private void EvictOldestEntries()
    {
        // Find and remove entries older than cache expiry first
        var now = DateTimeOffset.Now;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            if (now - kvp.Value.CachedAt > CacheExpiry)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        // Remove expired entries
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        // If still over capacity after removing expired entries, remove oldest entry
        if (_cache.Count >= MaxCacheSize)
        {
            var oldestKey = _cache
                .OrderBy(kvp => kvp.Value.CachedAt)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (oldestKey != null)
            {
                _cache.TryRemove(oldestKey, out _);
            }
        }
    }

    /// <summary>
    ///     Represents a cached search result entry.
    /// </summary>
    private sealed class CacheEntry
    {
        public required IReadOnlyList<ModDatabaseSearchResult> Results { get; init; }
        public required DateTimeOffset CachedAt { get; init; }
    }
}
