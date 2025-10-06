using System;
using System.Globalization;
using System.Windows.Data;

namespace VintageStoryModManager.Converters;

/// <summary>
/// Converts an element height into an offset that can be applied to translation animations.
/// </summary>
public sealed class HeightWithOffsetConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the additional offset that should always be applied.
    /// </summary>
    public double Offset { get; set; }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double height = value switch
        {
            double doubleValue when !double.IsNaN(doubleValue) => doubleValue,
            float floatValue => floatValue,
            string stringValue when double.TryParse(stringValue, NumberStyles.Float, culture, out var parsedValue) => parsedValue,
            _ => 0d,
        };

        return height + Offset;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
