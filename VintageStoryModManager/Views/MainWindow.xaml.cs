using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize the mod manager:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void OnWebsiteNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        Uri? target = e.Uri;
        if (target == null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open link:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_isInitialized || _viewModel == null)
        {
            return;
        }

        _isInitialized = true;
        await InitializeAsync(_viewModel);
    }

    private static async Task InitializeAsync(MainViewModel viewModel)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }


}
