using System.IO;
using System.Text.Json;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides disk-backed caching for mod manifests and icons so zip archives do not need to be reopened repeatedly.
///     All cache entries are stored in a single file for improved IO performance.
/// </summary>
internal static class ModManifestCacheService
{
    private static readonly object IndexLock = new();
    private static Dictionary<string, CacheEntry>? _index;

    public static bool TryGetManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        out string manifestJson)
    {
        manifestJson = string.Empty;

        var normalizedPath = NormalizePath(sourcePath);
        var ticks = ToUniversalTicks(lastWriteTimeUtc);

        lock (IndexLock)
        {
            var index = EnsureIndexLocked();
            if (!index.TryGetValue(normalizedPath, out var entry)) return false;

            if (entry.Length != length || entry.LastWriteTimeUtcTicks != ticks)
            {
                index.Remove(normalizedPath);
                SaveIndexLocked(index);
                return false;
            }

            try
            {
                manifestJson = entry.ManifestJson ?? string.Empty;
                return !string.IsNullOrWhiteSpace(manifestJson);
            }
            catch (Exception)
            {
                index.Remove(normalizedPath);
                SaveIndexLocked(index);
                return false;
            }
        }
    }

    public static void ClearCache()
    {
        lock (IndexLock)
        {
            _index = null;
        }

        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath)) return;

        try
        {
            if (File.Exists(cacheFilePath))
                File.Delete(cacheFilePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete the mod metadata cache at {cacheFilePath}.", ex);
        }
    }

    public static void StoreManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        string modId,
        string? version,
        string manifestJson)
    {
        var normalizedPath = NormalizePath(sourcePath);
        var ticks = ToUniversalTicks(lastWriteTimeUtc);

        try
        {
            lock (IndexLock)
            {
                var index = EnsureIndexLocked();
                if (!index.TryGetValue(normalizedPath, out var entry))
                {
                    entry = new CacheEntry();
                    index[normalizedPath] = entry;
                }

                entry.ModId = modId;
                entry.Version = version;
                entry.ManifestJson = manifestJson;
                entry.Length = length;
                entry.LastWriteTimeUtcTicks = ticks;

                SaveIndexLocked(index);
            }
        }
        catch (Exception)
        {
            // Intentionally swallow errors; cache failures should not impact mod loading.
        }
    }

    public static void Invalidate(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);
        lock (IndexLock)
        {
            var index = EnsureIndexLocked();
            if (index.Remove(normalizedPath)) SaveIndexLocked(index);
        }
    }

    private static Dictionary<string, CacheEntry> EnsureIndexLocked()
    {
        return _index ??= LoadIndex();
    }

    private static Dictionary<string, CacheEntry> LoadIndex()
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath) || !File.Exists(cacheFilePath))
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var model = JsonSerializer.Deserialize<ModManifestCache>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (model?.Entries == null)
                return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

            return model.Entries;
        }
        catch (Exception)
        {
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveIndexLocked(Dictionary<string, CacheEntry> index)
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath)) return;

        try
        {
            var directory = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var model = new ModManifestCache
            {
                Entries = index
            };

            var json = JsonSerializer.Serialize(model);
            var tempPath = cacheFilePath + ".tmp";
            
            File.WriteAllText(tempPath, json);
            
            try
            {
                File.Move(tempPath, cacheFilePath, true);
            }
            catch (IOException)
            {
                try
                {
                    // Retry with replace semantics when running on platforms that require it.
                    File.Replace(tempPath, cacheFilePath, null);
                }
                catch
                {
                    // Clean up temp file only if replace also failed
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    throw;
                }
            }
        }
        catch (Exception)
        {
            // Ignore cache persistence failures.
        }
    }

    private static string? GetCacheFilePath()
    {
        var baseDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        return Path.Combine(baseDirectory, "mod-metadata-cache.json");
    }

    private static string NormalizePath(string sourcePath)
    {
        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (Exception)
        {
            return sourcePath;
        }
    }

    private static long ToUniversalTicks(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Local);

        return value.ToUniversalTime().Ticks;
    }

    private sealed class ModManifestCache
    {
        public Dictionary<string, CacheEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CacheEntry
    {
        public string ModId { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? ManifestJson { get; set; }
        public long Length { get; set; }
        public long LastWriteTimeUtcTicks { get; set; }
    }
}