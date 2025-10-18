using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VintageStoryModManager.Services;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "VintageStoryModManager.SingleInstance";
    private static readonly Uri LightThemeUri = new("Resources/Themes/LightTheme.xaml", UriKind.Relative);
    private static readonly Uri DarkVsThemeUri = new("Resources/Themes/DarkVsTheme.xaml", UriKind.Relative);
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private ResourceDictionary? _activeTheme;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        _instanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            ActivateExistingInstance();
            Current?.Shutdown();
            return;
        }

        ApplyPreferredTheme();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        if (_instanceMutex != null)
        {
            if (_ownsMutex)
            {
                _instanceMutex.ReleaseMutex();
            }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            _ownsMutex = false;
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string message = $"An unexpected error occurred:\n{e.Exception.Message}";
        WpfMessageBox.Show(message, "Simple VS Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void ActivateExistingInstance()
    {
        try
        {
            Process currentProcess = Process.GetCurrentProcess();

            foreach (Process process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id == currentProcess.Id)
                {
                    continue;
                }

                IntPtr handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                WindowActivator.ShowAndActivate(handle);
                break;
            }
        }
        catch (Exception)
        {
            // If we cannot activate the existing instance we silently continue shutting down.
        }
    }

    private static class WindowActivator
    {
        private const int SwRestore = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void ShowAndActivate(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(windowHandle, SwRestore);
            SetForegroundWindow(windowHandle);
        }
    }

    private void ApplyPreferredTheme()
    {
        bool useDarkVsMode = false;

        try
        {
            var configuration = new UserConfigurationService();
            useDarkVsMode = configuration.UseDarkVsMode;
        }
        catch (Exception)
        {
            // If the configuration fails to load we silently fall back to the default theme.
        }

        if (_activeTheme is null && Resources is { MergedDictionaries.Count: > 0 })
        {
            foreach (ResourceDictionary dictionary in Resources.MergedDictionaries)
            {
                string? source = dictionary.Source?.OriginalString;
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                if (source.Contains("Resources/Themes/", StringComparison.OrdinalIgnoreCase))
                {
                    _activeTheme = dictionary;
                    break;
                }
            }
        }

        ApplyThemeDictionary(useDarkVsMode ? DarkVsThemeUri : LightThemeUri);
    }

    private void ApplyThemeDictionary(Uri source)
    {
        if (Resources is null)
        {
            return;
        }

        if (_activeTheme != null)
        {
            Resources.MergedDictionaries.Remove(_activeTheme);
            _activeTheme = null;
        }

        try
        {
            var dictionary = new ResourceDictionary { Source = source };
            Resources.MergedDictionaries.Add(dictionary);
            _activeTheme = dictionary;
        }
        catch (Exception)
        {
            // If loading the preferred theme fails, fall back to the light theme.
            if (!ReferenceEquals(source, LightThemeUri))
            {
                ApplyThemeDictionary(LightThemeUri);
            }
        }
    }
}
