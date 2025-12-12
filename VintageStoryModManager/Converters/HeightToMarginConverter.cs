using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VintageStoryModManager.Converters;

public class HeightToMarginConverter : IValueConverter
{
    // Optional: make 226 configurable
    public double TargetHeight { get; set; } = 226;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double actualHeight && actualHeight > 0)
        {
            // Allow overriding TargetHeight via ConverterParameter if you want
            double target = TargetHeight;
            if (parameter != null &&
                double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
            {
                target = p;
            }

            // We want margin = (target - height) / 2 for smaller images
            // and 0 for larger ones
            double diff = target - actualHeight;
            double margin = diff > 0 ? diff / 2.0 : 0.0;

            return new Thickness(8, margin, 8, 0); // top only
        }

        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
