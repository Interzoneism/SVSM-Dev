using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides disk-backed caching for mod icons as separate image files.
///     Icons are stored in individual files named by a hash of the source path for quick lookup.
/// </summary>
internal static class ModIconCacheService
{
    private static readonly object CacheLock = new();

    /// <summary>
    /// Gets the icon cache directory path.
    /// </summary>
    public static string? GetIconCacheDirectory()
    {
        var baseDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        return Path.Combine(baseDirectory, "mod-icon-cache");
    }

    /// <summary>
    /// Tries to get the cached icon file path for a mod.
    /// </summary>
    public static bool TryGetIconPath(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        out string? iconPath)
    {
        iconPath = null;

        var cacheDirectory = GetIconCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return false;

        var cacheFileName = GetCacheFileName(sourcePath, lastWriteTimeUtc, length);
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);

        if (!File.Exists(cachePath)) return false;

        iconPath = cachePath;
        return true;
    }

    /// <summary>
    /// Stores an icon in the cache.
    /// </summary>
    public static void StoreIcon(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        byte[] iconBytes)
    {
        if (iconBytes == null || iconBytes.Length == 0) return;

        var cacheDirectory = GetIconCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return;

        try
        {
            lock (CacheLock)
            {
                Directory.CreateDirectory(cacheDirectory);

                var cacheFileName = GetCacheFileName(sourcePath, lastWriteTimeUtc, length);
                var cachePath = Path.Combine(cacheDirectory, cacheFileName);

                // Don't overwrite if it already exists
                if (File.Exists(cachePath)) return;

                var tempPath = cachePath + ".tmp";
                File.WriteAllBytes(tempPath, iconBytes);

                try
                {
                    File.Move(tempPath, cachePath, false);
                }
                catch (IOException)
                {
                    // If the file was created concurrently, clean up temp file
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    
                    // If target exists, that's fine, ignore the error
                    if (!File.Exists(cachePath)) throw;
                }
            }
        }
        catch (Exception)
        {
            // Intentionally swallow errors; cache failures should not impact mod loading.
        }
    }

    /// <summary>
    /// Invalidates the cached icon for a specific source path.
    /// </summary>
    public static void Invalidate(string sourcePath)
    {
        var cacheDirectory = GetIconCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return;

        try
        {
            lock (CacheLock)
            {
                // Find and delete all cached icons for this source path
                var prefix = GetCacheFilePrefix(sourcePath);
                if (!Directory.Exists(cacheDirectory)) return;

                foreach (var file in Directory.EnumerateFiles(cacheDirectory, prefix + "*"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // Ignore individual file deletion failures
                    }
                }
            }
        }
        catch (Exception)
        {
            // Intentionally swallow errors
        }
    }

    /// <summary>
    /// Clears the entire icon cache.
    /// </summary>
    public static void ClearCache()
    {
        var cacheDirectory = GetIconCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return;

        try
        {
            lock (CacheLock)
            {
                if (Directory.Exists(cacheDirectory))
                    Directory.Delete(cacheDirectory, true);
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete the icon cache at {cacheDirectory}.", ex);
        }
    }

    private static string GetCacheFileName(string sourcePath, DateTime lastWriteTimeUtc, long length)
    {
        var prefix = GetCacheFilePrefix(sourcePath);
        var ticks = ToUniversalTicks(lastWriteTimeUtc);
        return $"{prefix}_{ticks}_{length}.png";
    }

    private static string GetCacheFilePrefix(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);
        
        // Use SHA256 to create a stable hash of the path
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        
        // Convert to hex string (take first 16 bytes for reasonable length)
        var builder = new StringBuilder(32);
        for (var i = 0; i < Math.Min(16, hashBytes.Length); i++)
        {
            builder.Append(hashBytes[i].ToString("x2"));
        }
        
        return builder.ToString();
    }

    private static string NormalizePath(string sourcePath)
    {
        try
        {
            return Path.GetFullPath(sourcePath).ToLowerInvariant();
        }
        catch (Exception)
        {
            return sourcePath.ToLowerInvariant();
        }
    }

    private static long ToUniversalTicks(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified) value = DateTime.SpecifyKind(value, DateTimeKind.Local);

        return value.ToUniversalTime().Ticks;
    }
}
