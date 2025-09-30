using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VintageStoryModManager.Services;

/// <summary>
/// Stores simple user configuration values for the mod manager, such as the selected directories.
/// </summary>
public sealed class UserConfigurationService
{
    private const string ConfigurationFileName = "settings.json";
    private readonly string _configurationPath;
    private readonly Dictionary<string, List<string>> _presets = new(StringComparer.OrdinalIgnoreCase);

    public UserConfigurationService()
    {
        _configurationPath = DetermineConfigurationPath();
        Load();
    }

    public string? DataDirectory { get; private set; }

    public string? GameDirectory { get; private set; }

    public string? SelectedPreset { get; private set; }

    public void SetDataDirectory(string path)
    {
        DataDirectory = NormalizePath(path);
        Save();
    }

    public void SetGameDirectory(string path)
    {
        GameDirectory = NormalizePath(path);
        Save();
    }

    public IEnumerable<string> GetPresetNames()
    {
        return _presets.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool ContainsPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return FindExistingPresetKey(name.Trim()) != null;
    }

    public bool TryGetPreset(string name, out string actualName, out IReadOnlyList<string> disabledEntries)
    {
        actualName = string.Empty;
        disabledEntries = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string? key = FindExistingPresetKey(name.Trim());
        if (key is null)
        {
            return false;
        }

        actualName = key;
        disabledEntries = _presets[key].AsReadOnly();
        return true;
    }

    public string SetPreset(string name, IEnumerable<string> disabledEntries)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(disabledEntries);

        string trimmedName = name.Trim();
        string key = FindExistingPresetKey(trimmedName) ?? trimmedName;

        var entries = new List<string>();
        var lookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string entry in disabledEntries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            string trimmed = entry.Trim();
            if (lookup.Add(trimmed))
            {
                entries.Add(trimmed);
            }
        }

        _presets[key] = entries;
        Save();
        return key;
    }

    public void SetSelectedPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SelectedPreset = null;
        }
        else
        {
            string? key = FindExistingPresetKey(name.Trim());
            SelectedPreset = key;
        }

        Save();
    }

    private void Load()
    {
        _presets.Clear();
        SelectedPreset = null;

        try
        {
            if (!File.Exists(_configurationPath))
            {
                return;
            }

            using FileStream stream = File.OpenRead(_configurationPath);
            JsonNode? node = JsonNode.Parse(stream);
            if (node is not JsonObject obj)
            {
                return;
            }

            DataDirectory = NormalizePath(obj["dataDirectory"]?.GetValue<string?>());
            GameDirectory = NormalizePath(obj["gameDirectory"]?.GetValue<string?>());

            if (obj["presets"] is JsonObject presetsObject)
            {
                foreach (var pair in presetsObject)
                {
                    string? name = pair.Key;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string trimmedName = name.Trim();
                    string key = FindExistingPresetKey(trimmedName) ?? trimmedName;

                    var entries = new List<string>();
                    var lookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (pair.Value is JsonArray array)
                    {
                        foreach (JsonNode? item in array)
                        {
                            string? entry = item?.GetValue<string?>();
                            if (string.IsNullOrWhiteSpace(entry))
                            {
                                continue;
                            }

                            string trimmedEntry = entry.Trim();
                            if (lookup.Add(trimmedEntry))
                            {
                                entries.Add(trimmedEntry);
                            }
                        }
                    }

                    _presets[key] = entries;
                }
            }

            string? selected = obj["selectedPreset"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SelectedPreset = FindExistingPresetKey(selected.Trim());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            DataDirectory = null;
            GameDirectory = null;
            _presets.Clear();
            SelectedPreset = null;
        }
    }

    private void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(_configurationPath)!;
            Directory.CreateDirectory(directory);

            var obj = new JsonObject
            {
                ["dataDirectory"] = DataDirectory,
                ["gameDirectory"] = GameDirectory
            };

            if (_presets.Count > 0)
            {
                var presetsObject = new JsonObject();
                foreach (var pair in _presets.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var array = new JsonArray();
                    foreach (string item in pair.Value)
                    {
                        array.Add(item);
                    }

                    presetsObject[pair.Key] = array;
                }

                obj["presets"] = presetsObject;
            }

            if (!string.IsNullOrWhiteSpace(SelectedPreset) && FindExistingPresetKey(SelectedPreset) is string key)
            {
                obj["selectedPreset"] = key;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(_configurationPath, obj.ToJsonString(options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the configuration is a best-effort attempt. Ignore failures silently.
        }
    }

    private string? FindExistingPresetKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (string key in _presets.Keys)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static string DetermineConfigurationPath()
    {
        string baseDirectory = GetBaseDirectory();
        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, ConfigurationFileName);
    }

    private static string GetBaseDirectory()
    {
        string? appData = GetFolder(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            return Path.Combine(appData!, "VintageStoryModManager");
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home!, ".config", "VintageStoryModManager");
        }

        return Path.Combine(AppContext.BaseDirectory, "Config");
    }

    private static string? GetFolder(Environment.SpecialFolder folder)
    {
        try
        {
            string? path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
