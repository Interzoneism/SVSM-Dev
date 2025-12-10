using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace VintageStoryModManager.Services;

/// <summary>
/// Provides utilities to derive representative colors from mod logo images.
/// </summary>
internal static class ModImageColorAnalysisService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly ConcurrentDictionary<string, Color> ColorCache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<Color?> GetAverageColorAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        if (ColorCache.TryGetValue(imageUrl, out var cachedColor))
        {
            return cachedColor;
        }

        try
        {
            var imageBytes = await TryGetImageBytesAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            if (imageBytes == null || imageBytes.Length == 0) return null;

            var calculatedColor = await Task.Run(() => CalculateAverageColor(imageBytes), cancellationToken).ConfigureAwait(false);
            if (calculatedColor.HasValue)
            {
                ColorCache[imageUrl] = calculatedColor.Value;
            }

            return calculatedColor;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<byte[]?> TryGetImageBytesAsync(string imageUrl, CancellationToken cancellationToken)
    {
        var cached = ModImageCacheService.TryGetCachedImage(imageUrl);
        if (cached?.Length > 0)
        {
            return cached;
        }

        cached = await ModImageCacheService.TryGetCachedImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        if (cached?.Length > 0)
        {
            return cached;
        }

        var downloaded = await HttpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        _ = ModImageCacheService.StoreImageAsync(imageUrl, downloaded, cancellationToken);
        return downloaded;
    }

    private static Color? CalculateAverageColor(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
        bitmap.DecodePixelWidth = 64;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        var formatted = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        formatted.Freeze();

        if (formatted.PixelWidth == 0 || formatted.PixelHeight == 0)
        {
            return null;
        }

        var stride = formatted.PixelWidth * (formatted.Format.BitsPerPixel / 8);
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        long totalR = 0;
        long totalG = 0;
        long totalB = 0;
        long totalA = 0;
        var pixelCount = formatted.PixelWidth * formatted.PixelHeight;

        for (var i = 0; i < pixels.Length; i += 4)
        {
            totalB += pixels[i];
            totalG += pixels[i + 1];
            totalR += pixels[i + 2];
            totalA += pixels[i + 3];
        }

        if (pixelCount == 0)
        {
            return null;
        }

        var avgA = totalA / pixelCount;
        var avgR = totalR / pixelCount;
        var avgG = totalG / pixelCount;
        var avgB = totalB / pixelCount;

        var alpha = avgA == 0 ? byte.MaxValue : (byte)Math.Min(byte.MaxValue, avgA);

        return Color.FromArgb(
            alpha,
            (byte)Math.Min(byte.MaxValue, avgR),
            (byte)Math.Min(byte.MaxValue, avgG),
            (byte)Math.Min(byte.MaxValue, avgB));
    }
}
