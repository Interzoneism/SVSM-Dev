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
    ///     Gets the cached thumbnail image for a mod, downloading it if necessary.
    /// </summary>
    /// <param name="modId">The mod ID</param>
    /// <param name="thumbnailUrl">The URL to the thumbnail image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A BitmapImage if successful, null otherwise</returns>
    public async Task<BitmapImage?> GetThumbnailAsync(
        string modId,
        string? thumbnailUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(thumbnailUrl))
            return null;

        // Sanitize mod ID for filename
        var safeModId = SanitizeFileName(modId);
        var cachePath = Path.Combine(_cacheDirectory, $"{safeModId}_thumbnail.png");

        // Try to load from cache first
        if (File.Exists(cachePath))
        {
            try
            {
                return LoadImageFromFile(cachePath);
            }
            catch
            {
                // If cached file is corrupted, delete and re-download
                File.Delete(cachePath);
            }
        }

        // Download the image
        try
        {
            using var response = await _httpClient.GetAsync(thumbnailUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            // Save to cache
            await File.WriteAllBytesAsync(cachePath, imageData, cancellationToken);

            // Load and return the image
            return LoadImageFromFile(cachePath);
        }
        catch
        {
            return null;
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
