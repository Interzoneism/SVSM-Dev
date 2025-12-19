using System;
using System.Globalization;
using System.Windows.Data;

namespace VintageStoryModManager.Converters
{
    /// <summary>
    /// Converts a boolean IsCompactView to a width value for DataGrid columns.
    /// Usage: ConverterParameter="80,40" (normal,compact)
    /// </summary>
    public class CompactViewWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isCompact = value is bool b && b;
            if (parameter is string param)
            {
                var parts = param.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double normal) &&
                    double.TryParse(parts[1], out double compact))
                {
                    return isCompact ? compact : normal;
                }
            }
            // Fallback: 80 for normal, 40 for compact
            return isCompact ? 40.0 : 80.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("CompactViewWidthConverter does not support ConvertBack.");
        }
    }
}