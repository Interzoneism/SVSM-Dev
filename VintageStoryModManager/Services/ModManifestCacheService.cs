using System.IO;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides disk-backed caching for mod manifests and icons so zip archives do not need to be reopened repeatedly.
///     Now uses SQLite backend via SqliteModCacheService with images stored in Temp Cache/Images/.
/// </summary>
internal static class ModManifestCacheService
{
    private static readonly object InitLock = new();
    private static SqliteModCacheService? _sqliteCache;

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

    public static bool TryGetManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        out string manifestJson,
        out byte[]? iconBytes)
    {
        var cache = GetCache();
        return cache.TryGetManifest(sourcePath, lastWriteTimeUtc, length, out manifestJson, out iconBytes);
    }

    public static void ClearCache()
    {
        var cache = GetCache();
        cache.ClearCache();
    }

    public static void StoreManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        string modId,
        string? version,
        string manifestJson,
        byte[]? iconBytes)
    {
        var cache = GetCache();
        cache.StoreManifest(sourcePath, lastWriteTimeUtc, length, modId, version, manifestJson, iconBytes);
    }

    public static void Invalidate(string sourcePath)
    {
        var cache = GetCache();
        cache.InvalidateManifest(sourcePath);
    }

    public static void UpdateTags(string modId, string? version, IReadOnlyList<string> tags)
    {
        var cache = GetCache();
        cache.UpdateTags(modId, version, tags);
    }

    public static IReadOnlyList<string> GetTags(string modId, string? version)
    {
        var cache = GetCache();
        return cache.GetTags(modId, version);
    }
}