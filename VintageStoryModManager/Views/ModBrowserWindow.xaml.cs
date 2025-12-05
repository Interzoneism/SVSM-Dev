using System.Net.Http;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

/// <summary>
/// Interaction logic for ModBrowserWindow.xaml
/// </summary>
public partial class ModBrowserWindow : Window
{
    public ModBrowserWindow()
    {
        InitializeComponent();

        // Set up dependency injection manually for simplicity
        // In a larger application, you would use a DI container
        var httpClient = new HttpClient();
        var modApiService = new ModApiService(httpClient);
        var viewModel = new ModBrowserViewModel(modApiService);

        ModBrowser.DataContext = viewModel;
    }
}