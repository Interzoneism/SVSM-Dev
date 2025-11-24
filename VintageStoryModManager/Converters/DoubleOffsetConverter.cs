using System.Globalization;
using System.Windows.Data;

namespace VintageStoryModManager.Converters;

/// <summary>
///     WPF value converter that adds a numeric offset to a base numeric value.
///     Useful for adjusting positions, sizes, or other numeric properties in XAML bindings.
/// </summary>
public class DoubleOffsetConverter : IValueConverter
{
    /// <summary>
    ///     Converts a base numeric value by adding an offset to it.
    /// </summary>
    /// <param name="value">The base numeric value (double, float, or int).</param>
    /// <param name="targetType">The type to convert to (double, int, float, or string).</param>
    /// <param name="parameter">The offset to add to the base value.</param>
    /// <param name="culture">The culture to use for conversion.</param>
    /// <returns>The base value plus the offset, converted to the target type.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var baseValue = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            null => 0d,
            _ => System.Convert.ToDouble(value, culture)
        };

        var offset = parameter switch
        {
            double d => d,
            float f => f,
            int i => i,
            null => 0d,
            _ => System.Convert.ToDouble(parameter, culture)
        };

        var result = baseValue + offset;

        if (targetType == typeof(string)) return result.ToString(culture);

        if (targetType == typeof(int)) return (int)Math.Round(result);

        if (targetType == typeof(float)) return (float)result;

        return result;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}