using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

/// <summary>
/// Interaction logic for ModBrowserView.xaml
/// </summary>
public partial class ModBrowserView : System.Windows.Controls.UserControl
{
    private Storyboard? _spinnerStoryboard;

    public ModBrowserView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnControlLoaded;
        Unloaded += OnControlUnloaded;
    }

    private ModBrowserViewModel? ViewModel => DataContext as ModBrowserViewModel;

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        // Cache the storyboard reference during initialization
        _spinnerStoryboard = TryFindResource("SpinnerAnimation") as Storyboard;
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up resources when the control is unloaded
        StopSpinnerAnimation();
        _spinnerStoryboard = null;

        if (DataContext is ModBrowserViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ModBrowserViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ModBrowserViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModBrowserViewModel.IsSearching))
        {
            Dispatcher.Invoke(() =>
            {
                if (ViewModel?.IsSearching == true)
                {
                    StartSpinnerAnimation();
                }
                else
                {
                    StopSpinnerAnimation();
                }
            });
        }
    }

    private void StartSpinnerAnimation()
    {
        _spinnerStoryboard?.Begin(this, true);
    }

    private void StopSpinnerAnimation()
    {
        _spinnerStoryboard?.Stop(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.InitializeCommand.ExecuteAsync(null);
        }
    }

    private void ScrollToTop_Click(object sender, RoutedEventArgs e)
    {
        ModsScrollViewer.ScrollToTop();
    }

    private void ModsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Load more mods when scrolling near the bottom
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - (e.ViewportHeight / 2 + 100))
        {
            ViewModel?.LoadMoreCommand.Execute(null);
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent card click
        if (sender is FrameworkElement element && element.Tag is int modId)
        {
            ViewModel?.ToggleFavoriteCommand.Execute(modId);
        }
    }

    private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent card click
        if (sender is FrameworkElement element && element.Tag is int assetId)
        {
            ViewModel?.OpenModInBrowserCommand.Execute(assetId);
        }
    }

    private void OpenInBrowserTitle_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement element && element.Tag is int assetId)
        {
            ViewModel?.OpenModInBrowserCommand.Execute(assetId);
        }
    }
}
