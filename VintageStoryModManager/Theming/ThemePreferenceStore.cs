using System;
using System.IO;

namespace VintageStoryModManager.Theming;

public static class ThemePreferenceStore
{
    private const string DirectoryName = "VintageStoryModManager";
    private const string FileName = "theme.config";

    public static AppTheme Load()
    {
        try
        {
            string? directory = GetConfigurationDirectory();
            if (string.IsNullOrWhiteSpace(directory))
            {
                return AppTheme.Modern;
            }

            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
            {
                return AppTheme.Modern;
            }

            string? content = File.ReadAllText(path)?.Trim();
            if (Enum.TryParse(content, ignoreCase: true, out AppTheme parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // Ignore and fall back to the default theme.
        }

        return AppTheme.Modern;
    }

    public static void Save(AppTheme theme)
    {
        string? directory = GetConfigurationDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Unable to resolve the configuration directory.");
        }

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        File.WriteAllText(path, theme.ToString());
    }

    private static string? GetConfigurationDirectory()
    {
        string? roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            return Path.Combine(roaming, DirectoryName);
        }

        string? local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return Path.Combine(local, DirectoryName);
        }

        return null;
    }
}

