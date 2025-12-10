using System.ComponentModel;
using System.Threading.Tasks;
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
    private bool _isInitialized;

    public ModBrowserView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnControlUnloaded;
    }

    private ModBrowserViewModel? ViewModel => DataContext as ModBrowserViewModel;

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        // Cache the storyboard reference during initialization
        _spinnerStoryboard = TryFindResource("SpinnerAnimation") as Storyboard;
    }

    public async Task InitializeIfNeededAsync()
    {
        if (_isInitialized)
            return;

        _spinnerStoryboard ??= TryFindResource("SpinnerAnimation") as Storyboard;

        if (ViewModel == null)
            return;

        _isInitialized = true;

        try
        {
            await ViewModel.InitializeCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the application
            System.Diagnostics.Debug.WriteLine($"[ModBrowserView] Error during initialization: {ex.Message}");
            // Reset flag to allow retry if initialization failed
            _isInitialized = false;
        }
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up resources when the control is unloaded
        StopSpinnerAnimation();
        _spinnerStoryboard = null;

        // Unsubscribe from events
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

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ModBrowserView] InstallButton_Click called");
        e.Handled = true; // Prevent card click
        
        if (ViewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ModBrowserView] ViewModel is null!");
            return;
        }
        
        if (sender is FrameworkElement element && element.Tag is int modId)
        {
            System.Diagnostics.Debug.WriteLine($"[ModBrowserView] Got modId: {modId}");
            
            if (ViewModel.InstallModCommand == null)
            {
                System.Diagnostics.Debug.WriteLine("[ModBrowserView] InstallModCommand is null!");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ModBrowserView] Executing InstallModCommand with modId: {modId}");
            ViewModel.InstallModCommand.Execute(modId);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ModBrowserView] Failed to get modId from Tag. Sender type: {sender?.GetType().Name}, Tag type: {(sender as FrameworkElement)?.Tag?.GetType().Name}");
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

    private async void VersionsDropdown_SelectionChanged(object? sender, EventArgs e)
    {
        await TriggerSearchOnSelectionChanged();
    }

    private async void TagsDropdown_SelectionChanged(object? sender, EventArgs e)
    {
        await TriggerSearchOnSelectionChanged();
    }

    private async Task TriggerSearchOnSelectionChanged()
    {
        // Trigger search when filter selection changes
        if (ViewModel != null)
        {
            await ViewModel.RefreshSearchAsync();
        }
    }
}
