using System;
using System.Windows;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Helpers;

/// <summary>
///     Attached property to trigger lazy loading of thumbnails when UI elements become loaded.
/// </summary>
public static class LazyThumbnailLoader
{
    public static readonly DependencyProperty LoadThumbnailProperty =
        DependencyProperty.RegisterAttached(
            "LoadThumbnail",
            typeof(bool),
            typeof(LazyThumbnailLoader),
            new PropertyMetadata(false, OnLoadThumbnailChanged));

    public static bool GetLoadThumbnail(DependencyObject obj)
    {
        return (bool)obj.GetValue(LoadThumbnailProperty);
    }

    public static void SetLoadThumbnail(DependencyObject obj, bool value)
    {
        obj.SetValue(LoadThumbnailProperty, value);
    }

    private static void OnLoadThumbnailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true)
            return;

        // Load thumbnail when element is loaded
        if (element.IsLoaded)
        {
            TriggerThumbnailLoad(element);
        }
        else
        {
            element.Loaded += OnElementLoaded;
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Loaded -= OnElementLoaded;
            TriggerThumbnailLoad(element);
        }
    }

    private static void TriggerThumbnailLoad(FrameworkElement element)
    {
        if (element.DataContext is ModListItemViewModel viewModel)
        {
            _ = viewModel.LoadThumbnailAsync();
        }
    }
}
