using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VintageStoryModManager.Services;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;
using Color = System.Windows.Media.Color;

namespace VintageStoryModManager;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "VintageStoryModManager.SingleInstance";
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
        ColorTheme theme = ColorTheme.VintageStory;
        IReadOnlyDictionary<string, string>? palette = null;

        try
        {
            var configuration = new UserConfigurationService();
            theme = configuration.ColorTheme;
            palette = configuration.GetThemePaletteColors();
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

        ApplyThemeDictionary(ResolveThemeUri(theme), palette);
    }

    private void ApplyThemeDictionary(Uri source, IReadOnlyDictionary<string, string>? paletteOverrides)
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
            if (paletteOverrides is not null)
            {
                ApplyPaletteOverrides(dictionary, paletteOverrides);
            }

            Resources.MergedDictionaries.Add(dictionary);
            _activeTheme = dictionary;
        }
        catch (Exception)
        {
            // If loading the preferred theme fails, fall back to the default theme.
            if (!ReferenceEquals(source, DarkVsThemeUri))
            {
                ApplyThemeDictionary(DarkVsThemeUri, paletteOverrides);
            }
        }
    }

    private static Uri ResolveThemeUri(ColorTheme theme)
    {
        return DarkVsThemeUri;
    }

    private static void ApplyPaletteOverrides(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> paletteOverrides)
    {
        foreach (var pair in paletteOverrides)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            if (!dictionary.Contains(pair.Key))
            {
                continue;
            }

            if (!TryParseColor(pair.Value, out Color color))
            {
                continue;
            }

            dictionary[pair.Key] = color;
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (!trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.Length <= 1)
        {
            return false;
        }

        string hex = trimmed[1..];
        if (hex.Length == 6)
        {
            if (TryParseHexByte(hex.Substring(0, 2), out byte r)
                && TryParseHexByte(hex.Substring(2, 2), out byte g)
                && TryParseHexByte(hex.Substring(4, 2), out byte b))
            {
                color = Color.FromRgb(r, g, b);
                return true;
            }

            return false;
        }

        if (hex.Length == 8)
        {
            if (TryParseHexByte(hex.Substring(0, 2), out byte a)
                && TryParseHexByte(hex.Substring(2, 2), out byte r)
                && TryParseHexByte(hex.Substring(4, 2), out byte g)
                && TryParseHexByte(hex.Substring(6, 2), out byte b))
            {
                color = Color.FromArgb(a, r, g, b);
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryParseHexByte(string hex, out byte value)
    {
        return byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
