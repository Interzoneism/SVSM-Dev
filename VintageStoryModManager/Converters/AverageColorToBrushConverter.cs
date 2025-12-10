using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VintageStoryModManager.Models;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace VintageStoryModManager.Converters;

/// <summary>
/// Converts an analyzed logo color into a brush suitable for card backgrounds.
/// </summary>
public class AverageColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fallbackBrush = Application.Current.TryFindResource("Brush.DataGrid.Header.Background") as Brush;
        fallbackBrush ??= new SolidColorBrush(DownloadableModOnList.NeutralLogoColor);

        if (value is not Color color || color.A == 0 || color == DownloadableModOnList.NeutralLogoColor)
        {
            return fallbackBrush;
        }

        var baseColor = Color.FromArgb(255, color.R, color.G, color.B);
        var accentColor = Color.FromArgb(
            255,
            (byte)Math.Min(byte.MaxValue, baseColor.R + 16),
            (byte)Math.Min(byte.MaxValue, baseColor.G + 16),
            (byte)Math.Min(byte.MaxValue, baseColor.B + 16));

        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(220, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb(235, accentColor.R, accentColor.G, accentColor.B), 1)
            }
        };

        gradientBrush.Freeze();
        return gradientBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
