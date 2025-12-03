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
public sealed class ThumbnailCacheService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _cacheDirectory;

    public ThumbnailCacheService()
    {
        // Cache directory: Temp Cache/Images/Thumbnails in Local AppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "SimpleVSManager");
        _cacheDirectory = Path.Combine(appFolder, "Temp Cache", "Images", "Thumbnails");

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

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
            using var response = await HttpClient.GetAsync(thumbnailUrl, cancellationToken);
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
    /// </summary>
    private static BitmapImage LoadImageFromFile(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
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
}
