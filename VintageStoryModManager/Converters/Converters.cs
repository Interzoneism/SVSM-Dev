using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using Color = System.Windows.Media.Color;

namespace VintageStoryModManager.Converters;



/// <summary>
/// Converts a null value to Visibility.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true;
        var isVisible = value != null;

        if (invert)
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}


/// <summary>
/// Converts a selected state to a border brush.
/// </summary>
public class SelectedToBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isSelected = value is bool boolValue && boolValue;
        return isSelected
            ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) // Blue
            : new SolidColorBrush(Color.FromArgb(13, 161, 161, 170)); // Transparent-ish
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Compares a value to a parameter for equality.
/// </summary>
public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return parameter;
        }
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts collection count to visibility (visible if count > 0).
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true;
        var count = value switch
        {
            int i => i,
            ICollection c => c.Count,
            _ => 0
        };

        var isVisible = count > 0;
        if (invert)
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Checks if an item is contained in a collection.
/// Used with MultiBinding: values[0] = item, values[1] = collection.
/// </summary>
public class ContainsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return false;

        var item = values[0];
        var collection = values[1];

        if (collection == null)
            return false;

        // Try to use IList.Contains for better performance
        if (collection is IList list)
        {
            return list.Contains(item);
        }

        // Fallback to enumeration for other IEnumerable types
        if (collection is IEnumerable enumerable)
        {
            foreach (var obj in enumerable)
            {
                if (Equals(obj, item))
                    return true;
            }
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a boolean to a background brush color (for favorite button styling).
/// </summary>
public class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is bool boolValue && boolValue;
        return isTrue
            ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) // Warning/Gold color
            : new SolidColorBrush(Color.FromRgb(9, 9, 11)); // Dark background
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a resource key string to a Geometry resource.
/// Used to dynamically load icon geometries from resource dictionary.
/// </summary>
public class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string iconKey && !string.IsNullOrEmpty(iconKey))
        {
            // Try to find the resource in the application resources
            if (Application.Current.TryFindResource(iconKey) is Geometry geometry)
            {
                return geometry;
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Multi-value converter for "No results" visibility.
/// Shows "No results" only when: not searching AND count is zero.
/// values[0] = IsSearching (bool), values[1] = count (int or ICollection)
/// </summary>
public class NoResultsVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return Visibility.Collapsed;

        var isSearching = values[0] is bool b && b;
        var count = values[1] switch
        {
            int i => i,
            ICollection c => c.Count,
            _ => 0
        };

        // Only show "No results" when not searching AND count is zero
        return !isSearching && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Multi-value converter for favorite button visibility.
/// Shows button when: card is hovered OR mod is favorited.
/// values[0] = IsMouseOver (bool), values[1] = ModId (object), values[2] = FavoriteMods collection
/// </summary>
public class FavoriteButtonVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] == DependencyProperty.UnsetValue || 
            values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue)
            return Visibility.Collapsed;

        var isMouseOver = values[0] is bool b && b;
        var modId = values[1];
        var favoriteMods = values[2];

        // Check if mod is favorited
        var isFavorited = false;
        if (favoriteMods is IList list && modId != null)
        {
            isFavorited = list.Contains(modId);
        }
        else if (favoriteMods is IEnumerable enumerable && modId != null)
        {
            foreach (var obj in enumerable)
            {
                if (Equals(obj, modId))
                {
                    isFavorited = true;
                    break;
                }
            }
        }

        // Show button when hovered OR favorited
        return isMouseOver || isFavorited ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
