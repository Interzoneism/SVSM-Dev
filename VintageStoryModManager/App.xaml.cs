using System;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views;

namespace VintageStoryModManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string dataDirectory;
        try
        {
            dataDirectory = DataDirectoryLocator.Resolve();
        }
        catch (Exception ex)
        {
            dataDirectory = Environment.CurrentDirectory;
            Console.Error.WriteLine($"Failed to resolve data directory, falling back to '{dataDirectory}': {ex.Message}");
        }

        var settingsStore = new ClientSettingsStore(dataDirectory);
        var discoveryService = new ModDiscoveryService(settingsStore);

        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(discoveryService, settingsStore)
        };

        window.Show();
    }
}
