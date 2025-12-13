using System.Collections.Concurrent;
using System.Collections.Generic;
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
    ///     Attempts to retrieve a cached image for the given URL synchronously.
    ///     This method does not use file locking and is intended for UI thread usage
    ///     where blocking is acceptable but deadlocks must be avoided.
    /// </summary>
    /// <param name="imageUrl">The URL of the image.</param>
    /// <param name="descriptor">Additional details used to generate a readable cache name.</param>
    /// <returns>The cached image bytes, or null if not cached.</returns>
    public static byte[]? TryGetCachedImage(string imageUrl, ModImageCacheDescriptor? descriptor = null)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var cachePath = GetCachePath(imageUrl, descriptor, out var legacyPath);

        var resolvedPath = ResolveCachePath(cachePath, legacyPath, descriptor is not null);

        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)) return null;

        try
        {
            return File.ReadAllBytes(resolvedPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Attempts to retrieve a cached image for the given URL.
    /// </summary>
    /// <param name="imageUrl">The URL of the image.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="descriptor">Additional details used to generate a readable cache name.</param>
    /// <returns>The cached image bytes, or null if not cached.</returns>
    public static async Task<byte[]?> TryGetCachedImageAsync(
        string imageUrl,
        CancellationToken cancellationToken,
        ModImageCacheDescriptor? descriptor = null)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var cachePath = GetCachePath(imageUrl, descriptor, out var legacyPath);

        var resolvedPath = await ResolveCachePathAsync(cachePath, legacyPath, descriptor is not null, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)) return null;

        var fileLock = await AcquireLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(resolvedPath)) return null;

            return await File.ReadAllBytesAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
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
    /// <param name="descriptor">Additional details used to generate a readable cache name.</param>
    public static async Task StoreImageAsync(
        string imageUrl,
        byte[] imageBytes,
        CancellationToken cancellationToken,
        ModImageCacheDescriptor? descriptor = null)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        if (imageBytes is null || imageBytes.Length == 0) return;

        var cachePath = GetCachePath(imageUrl, descriptor, out _);
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

    private static string? GetCachePath(
        string imageUrl,
        ModImageCacheDescriptor? descriptor,
        out string? legacyPath)
    {
        legacyPath = null;

        var cacheDirectory = ModCacheLocator.GetModDatabaseImageCacheDirectory();
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return null;

        var fileName = GenerateCacheFileName(imageUrl, descriptor);
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        if (descriptor is not null)
        {
            var legacyName = GenerateLegacyCacheFileName(imageUrl);
            if (!string.IsNullOrWhiteSpace(legacyName)) legacyPath = Path.Combine(cacheDirectory, legacyName);
        }

        return Path.Combine(cacheDirectory, fileName);
    }

    private static string ResolveCachePath(string? preferredPath, string? legacyPath, bool attemptMigration)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath)) return preferredPath;

        if (attemptMigration
            && !string.IsNullOrWhiteSpace(preferredPath)
            && !File.Exists(preferredPath)
            && !string.IsNullOrWhiteSpace(legacyPath)
            && File.Exists(legacyPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(preferredPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

                File.Copy(legacyPath, preferredPath, false);
                return preferredPath;
            }
            catch (Exception)
            {
                // Ignore migration failures, fall back to legacy path if available.
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath)) return legacyPath;

        return preferredPath ?? legacyPath ?? string.Empty;
    }

    private static async Task<string> ResolveCachePathAsync(
        string? preferredPath,
        string? legacyPath,
        bool attemptMigration,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath)) return preferredPath;

        if (attemptMigration
            && !string.IsNullOrWhiteSpace(preferredPath)
            && !File.Exists(preferredPath)
            && !string.IsNullOrWhiteSpace(legacyPath)
            && File.Exists(legacyPath))
        {
            var fileLock = await AcquireLockAsync(preferredPath, cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(preferredPath)) return preferredPath;

                var directory = Path.GetDirectoryName(preferredPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

                File.Copy(legacyPath, preferredPath, false);
                return preferredPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Ignore migration failures, fall back to legacy path if available.
            }
            finally
            {
                fileLock.Release();
            }
        }
      
        if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath)) return legacyPath;

        return preferredPath ?? legacyPath ?? string.Empty;
    }

    private static string GenerateCacheFileName(string imageUrl, ModImageCacheDescriptor? descriptor)
    {
        var extension = GetImageExtension(imageUrl);

        if (descriptor is null)
            return GenerateLegacyCacheFileName(imageUrl, extension);

        var segments = new List<string>();

        var sourceSegment = NormalizeSegment(descriptor.ApiSource);
        if (!string.IsNullOrWhiteSpace(sourceSegment)) segments.Add(sourceSegment);

        var modSegment = NormalizeSegment(descriptor.ModId ?? descriptor.ModName);
        if (!string.IsNullOrWhiteSpace(modSegment)) segments.Add(modSegment);

        if (segments.Count == 0) return GenerateLegacyCacheFileName(imageUrl, extension);

        return string.Concat(string.Join('_', segments), extension);
    }

    private static string GenerateLegacyCacheFileName(string imageUrl)
    {
        var extension = GetImageExtension(imageUrl);
        return GenerateLegacyCacheFileName(imageUrl, extension);
    }

    private static string GenerateLegacyCacheFileName(string imageUrl, string extension)
    {
        var hash = ComputeUrlHash(imageUrl);

        return string.IsNullOrWhiteSpace(extension) ? hash : $"{hash}{extension}";
    }

    private static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sanitized = ModCacheLocator.SanitizeFileName(value, "image");
        var normalized = sanitized.Replace(' ', '_').Trim('_');

        return string.IsNullOrWhiteSpace(normalized)
            ? string.Empty
            : normalized.ToLowerInvariant();
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

internal sealed record ModImageCacheDescriptor(string? ModId, string? ModName, string? ApiSource);
