using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides caching for mod database images to avoid repeated downloads.
/// </summary>
internal static class ModImageCacheService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = 
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Attempts to retrieve a cached image for the given URL.
    /// </summary>
    /// <param name="imageUrl">The URL of the image.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached image bytes, or null if not cached.</returns>
    public static async Task<byte[]?> GetCachedImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var cachePath = GetCachePath(imageUrl);
        if (string.IsNullOrWhiteSpace(cachePath)) return null;

        if (!File.Exists(cachePath)) return null;

        var fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(cachePath)) return null;

            return await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false);
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
    ///     Stores an image in the cache.
    /// </summary>
    /// <param name="imageUrl">The URL of the image.</param>
    /// <param name="imageBytes">The image data to cache.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public static async Task CacheImageAsync(string imageUrl, byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        if (imageBytes is null || imageBytes.Length == 0) return;

        var cachePath = GetCachePath(imageUrl);
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
            await File.WriteAllBytesAsync(tempPath, imageBytes, cancellationToken).ConfigureAwait(false);

            try
            {
                File.Move(tempPath, cachePath, true);
            }
            catch (IOException)
            {
                try
                {
                    File.Replace(tempPath, cachePath, null);
                }
                catch (Exception)
                {
                    // Clean up temp file only if Replace also fails
                    try
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                    catch (Exception)
                    {
                        // Ignore cleanup errors
                    }
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Ignore cache storage failures - caching is best effort.
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    ///     Clears all cached images.
    /// </summary>
    internal static void ClearCacheDirectory()
    {
        var cacheDirectory = ModCacheLocator.GetModDatabaseImageCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory) || !Directory.Exists(cacheDirectory)) return;

        try
        {
            Directory.Delete(cacheDirectory, true);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete the mod image cache at {cacheDirectory}.", ex);
        }
    }

    private static string? GetCachePath(string imageUrl)
    {
        var cacheDirectory = ModCacheLocator.GetModDatabaseImageCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return null;

        var fileName = GenerateCacheFileName(imageUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        return Path.Combine(cacheDirectory, fileName);
    }

    private static string GenerateCacheFileName(string imageUrl)
    {
        // Extract file extension from URL if available
        var extension = GetImageExtension(imageUrl);
        
        // Generate a hash of the URL for the filename to handle special characters
        var hash = ComputeUrlHash(imageUrl);
        
        return string.IsNullOrWhiteSpace(extension) ? hash : $"{hash}{extension}";
    }

    private static string GetImageExtension(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return ".png";

        try
        {
            // Remove query string if present
            var urlWithoutQuery = imageUrl;
            var queryIndex = imageUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                urlWithoutQuery = imageUrl.Substring(0, queryIndex);
            }

            var extension = Path.GetExtension(urlWithoutQuery);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                extension = extension.ToLowerInvariant();
                // Only use common image extensions
                if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
                {
                    return extension;
                }
            }
        }
        catch (Exception)
        {
            // Ignore parsing errors
        }

        return ".png";
    }

    private static string ComputeUrlHash(string url)
    {
        var bytes = Encoding.UTF8.GetBytes(url);
        var hashBytes = SHA256.HashData(bytes);
        
        // Use a URL-safe base64 encoding and take first 32 characters for a reasonable filename length
        var base64 = Convert.ToBase64String(hashBytes);
        var urlSafe = base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        return urlSafe.Length > 32 ? urlSafe.Substring(0, 32) : urlSafe;
    }

    private static async Task<SemaphoreSlim> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        var gate = FileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return gate;
    }
}
