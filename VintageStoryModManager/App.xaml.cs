using System;
using System.Windows;
using System.Windows.Threading;

namespace VintageStoryModManager;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string message = $"An unexpected error occurred:\n{e.Exception.Message}";
        MessageBox.Show(message, "Vintage Story Mod Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
