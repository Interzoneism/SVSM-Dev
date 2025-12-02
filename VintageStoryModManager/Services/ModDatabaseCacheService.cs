using System.IO;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides persistence for metadata retrieved from the Vintage Story mod database so that
///     subsequent requests can be served without repeatedly downloading large payloads.
///     Now uses SQLite backend via SqliteModCacheService.
/// </summary>
internal sealed class ModDatabaseCacheService
{
    private static readonly object InitLock = new();
    private static volatile SqliteModCacheService? _sqliteCache;

    private static SqliteModCacheService GetCache()
    {
        if (_sqliteCache != null) return _sqliteCache;

        lock (InitLock)
        {
            if (_sqliteCache == null)
            {
                _sqliteCache = new SqliteModCacheService();
                _sqliteCache.Initialize();
            }
            return _sqliteCache;
        }
    }

    internal static void ClearCacheDirectory()
    {
        var cache = GetCache();
        cache.ClearCache();
    }

    /// <summary>
    ///     Clears the in-memory cache. This is a no-op for SQLite backend but kept for API compatibility.
    /// </summary>
    internal void ClearInMemoryCache()
    {
        // SQLite implementation doesn't use in-memory cache
    }

    public async Task<ModDatabaseInfo?> TryLoadAsync(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion,
        bool allowExpiredEntryRefresh,
        bool requireExactVersionMatch,
        CancellationToken cancellationToken)
    {
        // allowExpiredEntryRefresh is no longer used with SQLite backend
        return await TryLoadWithoutExpiryAsync(
            modId,
            normalizedGameVersion,
            installedModVersion,
            requireExactVersionMatch,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Attempts to load cached mod database info from disk.
    ///     Cache entries are never expired by time - invalidation is based on HTTP conditional
    ///     requests performed by the caller using GetCachedHttpHeadersAsync.
    /// </summary>
    public Task<ModDatabaseInfo?> TryLoadWithoutExpiryAsync(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion,
        bool requireExactVersionMatch,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        var info = cache.TryLoadDatabaseInfo(modId, normalizedGameVersion, installedModVersion);
        return Task.FromResult(info);
    }

    /// <summary>
    ///     Attempts to load cached mod database info along with the lastmodified API value and cache timestamp.
    /// </summary>
    public Task<(ModDatabaseInfo? Info, string? LastModifiedApiValue, DateTimeOffset? CachedAt)> TryLoadWithLastModifiedAsync(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion,
        bool requireExactVersionMatch,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        var info = cache.TryLoadDatabaseInfo(modId, normalizedGameVersion, installedModVersion);
        var (lastModHeader, etag, lastModApi, cachedAt) = cache.GetCachedHttpHeaders(modId, normalizedGameVersion);
        return Task.FromResult((info, lastModApi, cachedAt));
    }

    /// <summary>
    ///     Gets the latest version stored in the cache for a mod, used for staleness checking.
    /// </summary>
    public Task<string?> GetCachedLatestVersionAsync(
        string modId,
        string? normalizedGameVersion,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        var version = cache.GetCachedLatestVersion(modId, normalizedGameVersion);
        return Task.FromResult(version);
    }

    /// <summary>
    ///     Gets the cached lastmodified value from the API and cache timestamp for cache invalidation.
    /// </summary>
    public Task<(string? LastModifiedApiValue, DateTimeOffset? CachedAt)> GetCachedLastModifiedAsync(
        string modId,
        string? normalizedGameVersion,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        var (_, _, lastModApi, cachedAt) = cache.GetCachedHttpHeaders(modId, normalizedGameVersion);
        return Task.FromResult((lastModApi, cachedAt));
    }

    /// <summary>
    ///     Checks if a cache entry requires a network refresh based on expiry rules.
    /// </summary>
    public Task<bool> CheckIfRefreshNeededAsync(
        string modId,
        string? normalizedGameVersion,
        bool useSoftExpiry,
        CancellationToken cancellationToken)
    {
        // If internet is disabled, no refresh is possible
        if (InternetAccessManager.IsInternetAccessDisabled) 
            return Task.FromResult(false);

        var cache = GetCache();
        var (_, _, lastModApi, cachedAt) = cache.GetCachedHttpHeaders(modId, normalizedGameVersion);

        // If cache doesn't exist, refresh is needed
        if (!cachedAt.HasValue)
            return Task.FromResult(true);

        // Time-based expiry for soft/hard checks
        var diskCacheSoftExpiry = TimeSpan.FromMinutes(5);
        var diskCacheHardExpiry = TimeSpan.FromHours(2);

        // If cache is older than hard expiry, force refresh
        if (DateTimeOffset.Now - cachedAt.Value > diskCacheHardExpiry)
            return Task.FromResult(true);

        // If no cached lastmodified value exists, we need to fetch data
        if (string.IsNullOrWhiteSpace(lastModApi))
            return Task.FromResult(true);

        // If soft expiry checking is enabled and cache is older than soft expiry, trigger refresh
        if (useSoftExpiry && DateTimeOffset.Now - cachedAt.Value > diskCacheSoftExpiry)
            return Task.FromResult(true);

        // Cache exists with lastmodified and hasn't expired
        return Task.FromResult(false);
    }

    /// <summary>
    ///     Gets the cached HTTP headers and cache timestamp for conditional request validation.
    /// </summary>
    public Task<(string? LastModified, string? ETag, DateTimeOffset? CachedAt)> GetCachedHttpHeadersAsync(
        string modId,
        string? normalizedGameVersion,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        var (lastModHeader, etag, _, cachedAt) = cache.GetCachedHttpHeaders(modId, normalizedGameVersion);
        return Task.FromResult((lastModHeader, etag, cachedAt));
    }

    public Task StoreAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        string? installedModVersion,
        CancellationToken cancellationToken)
    {
        return StoreAsync(modId, normalizedGameVersion, info, installedModVersion, null, null, null, cancellationToken);
    }

    public async Task StoreAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        string? installedModVersion,
        string? lastModifiedHeader,
        string? etag,
        CancellationToken cancellationToken)
    {
        await StoreAsync(modId, normalizedGameVersion, info, installedModVersion, lastModifiedHeader, etag, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StoreAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        string? installedModVersion,
        string? lastModifiedHeader,
        string? etag,
        string? lastModifiedApiValue,
        CancellationToken cancellationToken)
    {
        var cache = GetCache();
        await cache.StoreDatabaseInfoAsync(
            modId,
            normalizedGameVersion,
            info,
            installedModVersion,
            lastModifiedHeader,
            etag,
            lastModifiedApiValue,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the full file path to a cached logo image for a mod.
    /// </summary>
    public string? GetLogoPath(string modId, string? normalizedGameVersion)
    {
        var cache = GetCache();
        return cache.GetLogoPath(modId, normalizedGameVersion);
    }

    /// <summary>
    ///     Attempts to retrieve cached logo image bytes for a mod.
    /// </summary>
    public byte[]? TryGetLogoBytes(string modId, string? normalizedGameVersion)
    {
        var cache = GetCache();
        return cache.TryGetLogoBytes(modId, normalizedGameVersion);
    }
}
