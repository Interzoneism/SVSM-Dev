using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VintageStoryModManager.Services;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;
using Color = System.Windows.Media.Color;
using VintageStoryModManager.Views.Dialogs;

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
            ShowSingleInstanceWarning();
            ActivateExistingInstance();
            Current?.Shutdown();
            return;
        }

        UserConfigurationService? configuration = TryCreateConfiguration();

        if (configuration is not null && configuration.ConfigurationFileExists && configuration.IsLegacyConfiguration)
        {
            if (!PromptForLegacyConfigurationCleanup())
            {
                Current?.Shutdown();
                return;
            }

            IReadOnlyList<string> cleanupErrors = ClearLegacyManagerData();
            if (cleanupErrors.Count > 0)
            {
                string message = "Simple VS Manager could not remove some cached data:" +
                    "\n\n" + string.Join("\n", cleanupErrors);
                WpfMessageBox.Show(
                    message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        ApplyPreferredTheme(configuration);

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

        try
        {
            Current?.Shutdown(-1);
        }
        catch
        {
            // If shutdown fails we fall back to letting WPF terminate the process.
            e.Handled = false;
            return;
        }

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

    private static void ShowSingleInstanceWarning()
    {
        try
        {
            var buttonOverrides = new MessageDialogButtonContentOverrides
            {
                Ok = "Abort"
            };

            WpfMessageBox.Show(
                "Simple VS Manager is already running. This launch will be aborted.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                buttonContentOverrides: buttonOverrides);
        }
        catch
        {
            // We ignore failures showing the warning so the application can shut down cleanly.
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

    private static UserConfigurationService? TryCreateConfiguration()
    {
        try
        {
            return new UserConfigurationService();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool PromptForLegacyConfigurationCleanup()
    {
        try
        {
            var buttonOverrides = new MessageDialogButtonContentOverrides
            {
                Ok = "Continue",
                Cancel = "Exit"
            };

            MessageBoxResult result = WpfMessageBox.Show(
                "Simple VS Manager detected settings from an older version. " +
                "To make this update run smoothly we need to clear the manager's cached mods, metadata, " +
                "and database information. This will not delete any installed mods or other files.\n\n" +
                "Select Continue to remove the cached data now, or Exit to close the manager.",
                "Simple VS Manager",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information,
                buttonContentOverrides: buttonOverrides);

            return result == MessageBoxResult.OK;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static IReadOnlyList<string> ClearLegacyManagerData()
    {
        var errors = new List<string>();

        TryDeleteDirectory(ModCacheLocator.GetCachedModsDirectory(), errors);
        TryDeleteDirectory(ModCacheLocator.GetModDatabaseCacheDirectory(), errors);

        string? managerDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (!string.IsNullOrWhiteSpace(managerDirectory))
        {
            TryDeleteDirectory(Path.Combine(managerDirectory!, "Mod Metadata"), errors);
            TryDeleteDirectory(Path.Combine(managerDirectory!, "Modlists (Cloud Cache)"), errors);
            TryDeleteFile(Path.Combine(managerDirectory!, "SimpleVSManagerStatus.log"), errors);
        }

        TryDeleteFile(Path.Combine(AppContext.BaseDirectory, "SimpleVSManagerStatus.log"), errors);

        return errors;
    }

    private static void TryDeleteDirectory(string? path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or System.Security.SecurityException or PathTooLongException)
        {
            errors.Add($"{path}: {ex.Message}");
        }
    }

    private static void TryDeleteFile(string? path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or System.Security.SecurityException or PathTooLongException)
        {
            errors.Add($"{path}: {ex.Message}");
        }
    }

    private void ApplyPreferredTheme(UserConfigurationService? configuration)
    {
        ColorTheme theme = ColorTheme.VintageStory;
        IReadOnlyDictionary<string, string>? palette = null;

        try
        {
            UserConfigurationService? resolved = configuration ?? new UserConfigurationService();
            theme = resolved.ColorTheme;
            palette = resolved.GetThemePaletteColors();
        }
        catch (Exception)
        {
            // If the configuration fails to load we silently fall back to the default theme.
        }

        EnsureActiveThemeDictionary();
        ApplyTheme(theme, palette);
    }

    public static void ApplyTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverrides)
    {
        if (Current is not App app)
        {
            return;
        }

        app.EnsureActiveThemeDictionary();
        IReadOnlyDictionary<string, string>? overridesToApply = paletteOverrides is { Count: > 0 }
            ? paletteOverrides
            : null;
        app.ApplyThemeDictionary(ResolveThemeUri(theme), overridesToApply);
    }

    private void EnsureActiveThemeDictionary()
    {
        if (_activeTheme is not null || Resources is not { MergedDictionaries.Count: > 0 })
        {
            return;
        }

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
