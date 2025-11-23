using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides thread-safe caching for frozen ImageSource instances.
///     Images are decoded off the UI thread and frozen before caching.
/// </summary>
public sealed class ImageCacheService
{
    private static readonly Lazy<ImageCacheService> _instance = new(() => new ImageCacheService());
    
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the singleton instance of the ImageCacheService.
    /// </summary>
    public static ImageCacheService Instance => _instance.Value;

    private ImageCacheService()
    {
    }

    /// <summary>
    ///     Gets or creates a cached image from byte array data.
    ///     Decoding happens off the UI thread, and the result is frozen.
    /// </summary>
    /// <param name="bytes">Image data as byte array</param>
    /// <param name="cacheKey">Unique cache key for this image</param>
    /// <returns>Frozen ImageSource or null if decoding fails</returns>
    public ImageSource? GetOrCreateFromBytes(byte[] bytes, string cacheKey)
    {
        if (bytes == null || bytes.Length == 0) return null;
        if (string.IsNullOrWhiteSpace(cacheKey)) return DecodeImageFromBytes(bytes);

        return _cache.GetOrAdd(cacheKey, _ => DecodeImageFromBytes(bytes));
    }

    /// <summary>
    ///     Gets or creates a cached image from byte array data asynchronously.
    ///     Decoding happens off the UI thread, and the result is frozen.
    /// </summary>
    /// <param name="bytes">Image data as byte array</param>
    /// <param name="cacheKey">Unique cache key for this image</param>
    /// <returns>Frozen ImageSource or null if decoding fails</returns>
    public Task<ImageSource?> GetOrCreateFromBytesAsync(byte[] bytes, string cacheKey)
    {
        if (bytes == null || bytes.Length == 0) return Task.FromResult<ImageSource?>(null);
        if (string.IsNullOrWhiteSpace(cacheKey))
            return Task.Run(() => DecodeImageFromBytes(bytes));

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out var cached))
            return Task.FromResult(cached);

        // Decode off thread and cache result
        return Task.Run(() =>
        {
            return _cache.GetOrAdd(cacheKey, _ => DecodeImageFromBytes(bytes));
        });
    }

    /// <summary>
    ///     Gets or creates a cached image from a URI asynchronously.
    ///     Decoding happens off the UI thread, and the result is frozen.
    /// </summary>
    /// <param name="uri">URI to load image from</param>
    /// <returns>Frozen ImageSource or null if loading fails</returns>
    public Task<ImageSource?> GetOrCreateFromUriAsync(Uri uri)
    {
        if (uri == null) return Task.FromResult<ImageSource?>(null);

        var cacheKey = uri.AbsoluteUri;

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out var cached))
            return Task.FromResult(cached);

        // Decode off thread and cache result
        return Task.Run(() =>
        {
            return _cache.GetOrAdd(cacheKey, _ => DecodeImageFromUri(uri));
        });
    }

    /// <summary>
    ///     Clears all cached images.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    ///     Removes a specific image from cache.
    /// </summary>
    public bool RemoveFromCache(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return false;
        return _cache.TryRemove(cacheKey, out _);
    }

    /// <summary>
    ///     Decodes an image from byte array off the UI thread.
    ///     Uses BeginInit/EndInit pattern and freezes the result.
    /// </summary>
    private static ImageSource? DecodeImageFromBytes(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, false);
            var bitmap = new BitmapImage();
            
            // BeginInit/EndInit allows setting properties before loading
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Decode immediately
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            
            // Freeze to make thread-safe and allow cross-thread usage
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }
            
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Decodes an image from URI off the UI thread.
    ///     Uses BeginInit/EndInit pattern and freezes the result.
    /// </summary>
    private static ImageSource? DecodeImageFromUri(Uri uri)
    {
        try
        {
            var bitmap = new BitmapImage();
            
            // BeginInit/EndInit allows setting properties before loading
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Decode immediately
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            
            // Freeze to make thread-safe and allow cross-thread usage
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }
            
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
