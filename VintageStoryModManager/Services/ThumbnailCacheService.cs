using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace VintageStoryModManager.Services;

/// <summary>
///     Service for downloading and caching mod thumbnails from the database.
/// </summary>
public sealed class ThumbnailCacheService : IDisposable
{
    private static readonly Lazy<ThumbnailCacheService> LazyInstance = new(() => new ThumbnailCacheService());
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private bool _disposed;

    private ThumbnailCacheService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Cache directory: Temp Cache/Images/Thumbnails in Local AppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "Simple VS Manager");
        _cacheDirectory = Path.Combine(appFolder, "Temp Cache", "Images", "Thumbnails");

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    ///     Gets the singleton instance of the thumbnail cache service.
    /// </summary>
    public static ThumbnailCacheService Instance => LazyInstance.Value;

    /// <summary>
    ///     Gets the cached thumbnail image for a mod from cache only.
    ///     Does NOT download - thumbnails must be pre-downloaded using DownloadThumbnailAsync.
    /// </summary>
    /// <param name="modId">The mod ID</param>
    /// <returns>A BitmapImage if found in cache, null otherwise</returns>
    public BitmapImage? GetThumbnailFromCache(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return null;

        // Sanitize mod ID for filename
        var safeModId = SanitizeFileName(modId);
        var cachePath = Path.Combine(_cacheDirectory, $"{safeModId}_thumbnail.png");

        // Try to load from cache
        if (File.Exists(cachePath))
        {
            try
            {
                return LoadImageFromFile(cachePath);
            }
            catch
            {
                // If cached file is corrupted, delete it
                try
                {
                    File.Delete(cachePath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Downloads a thumbnail from the given URL and saves it to cache.
    ///     This is the ONLY method that should use thumbnail URLs.
    /// </summary>
    /// <param name="modId">The mod ID</param>
    /// <param name="thumbnailUrl">The URL to download the thumbnail from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if download was successful, false otherwise</returns>
    public async Task<bool> DownloadThumbnailAsync(
        string modId,
        string thumbnailUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(thumbnailUrl))
            return false;

        // Sanitize mod ID for filename
        var safeModId = SanitizeFileName(modId);
        var cachePath = Path.Combine(_cacheDirectory, $"{safeModId}_thumbnail.png");

        // Download the image
        try
        {
            using var response = await _httpClient.GetAsync(thumbnailUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            // Save to cache
            await File.WriteAllBytesAsync(cachePath, imageData, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Loads a BitmapImage from a file path.
    ///     Loads the file contents into memory to avoid file locking issues.
    /// </summary>
    private static BitmapImage LoadImageFromFile(string filePath)
    {
        // Read file bytes into memory to avoid keeping the file locked
        var imageBytes = File.ReadAllBytes(filePath);
        
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        
        // Use MemoryStream - it will be loaded into bitmap during EndInit with OnLoad cache option
        using (var stream = new MemoryStream(imageBytes))
        {
            bitmap.StreamSource = stream;
            bitmap.EndInit(); // Stream data is loaded here with OnLoad cache option
        }
        
        bitmap.Freeze(); // Make it thread-safe
        return bitmap;
    }

    /// <summary>
    ///     Sanitizes a mod ID to be safe for use as a filename.
    /// </summary>
    private static string SanitizeFileName(string modId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = modId;
        
        foreach (var c in invalid)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        return sanitized;
    }

    /// <summary>
    ///     Clears all cached thumbnails.
    /// </summary>
    public void ClearCache()
    {
        if (!Directory.Exists(_cacheDirectory))
            return;

        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*_thumbnail.png");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore individual file deletion errors
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient?.Dispose();
    }
}
