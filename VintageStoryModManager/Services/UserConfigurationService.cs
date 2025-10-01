using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Stores simple user configuration values for the mod manager, such as the selected directories.
/// </summary>
public sealed class UserConfigurationService
{
    private const string ConfigurationFileName = "settings.json";
    private readonly string _configurationPath;
    private readonly Dictionary<string, string[]> _presets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _modConfigPaths = new(StringComparer.OrdinalIgnoreCase);

    public UserConfigurationService()
    {
        _configurationPath = DetermineConfigurationPath();
        Load();
    }

    public string? DataDirectory { get; private set; }

    public string? GameDirectory { get; private set; }

    public bool TryGetModConfigPath(string? modId, out string? path)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            path = null;
            return false;
        }

        return _modConfigPaths.TryGetValue(modId.Trim(), out path);
    }

    public void SetModConfigPath(string modId, string path)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            throw new ArgumentException("Mod ID cannot be empty.", nameof(modId));
        }

        string? normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("The configuration path is invalid.", nameof(path));
        }

        _modConfigPaths[modId.Trim()] = normalized;
        Save();
    }

    public void RemoveModConfigPath(string? modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        if (_modConfigPaths.Remove(modId.Trim()))
        {
            Save();
        }
    }

    public IReadOnlyList<ModPreset> GetPresets()
    {
        return _presets
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ModPreset(pair.Key, pair.Value.ToArray()))
            .ToList();
    }

    public bool ContainsPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _presets.ContainsKey(name.Trim());
    }

    public bool TryGetPreset(string? name, out ModPreset? preset)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            preset = null;
            return false;
        }

        string normalized = name.Trim();
        if (_presets.TryGetValue(normalized, out string[]? entries))
        {
            preset = new ModPreset(normalized, entries.ToArray());
            return true;
        }

        preset = null;
        return false;
    }

    public void SetPreset(string name, IEnumerable<string> disabledEntries)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name cannot be empty.", nameof(name));
        }

        string normalized = name.Trim();

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (disabledEntries != null)
        {
            foreach (string entry in disabledEntries)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                string trimmed = entry.Trim();
                if (seen.Add(trimmed))
                {
                    values.Add(trimmed);
                }
            }
        }

        _presets[normalized] = values.Count == 0 ? Array.Empty<string>() : values.ToArray();
        Save();
    }

    public bool RemovePreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim();
        if (!_presets.Remove(normalized))
        {
            return false;
        }

        Save();
        return true;
    }

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

    private void Load()
    {
        _presets.Clear();
        _modConfigPaths.Clear();
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
            LoadPresets(obj["modPresets"]);
            LoadModConfigPaths(obj["modConfigPaths"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            DataDirectory = null;
            GameDirectory = null;
            _presets.Clear();
            _modConfigPaths.Clear();
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
                ["gameDirectory"] = GameDirectory,
                ["modPresets"] = BuildPresetsJson(),
                ["modConfigPaths"] = BuildModConfigPathsJson()
            };

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

    private static string DetermineConfigurationPath()
    {
        string baseDirectory = GetBaseDirectory();
        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, ConfigurationFileName);
    }

    private JsonObject BuildPresetsJson()
    {
        var result = new JsonObject();

        foreach (var pair in _presets.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var values = pair.Value;
            if (values.Length == 0)
            {
                result[pair.Key] = new JsonArray();
                continue;
            }

            var array = new JsonArray();
            foreach (string value in values)
            {
                array.Add(JsonValue.Create(value));
            }

            result[pair.Key] = array;
        }

        return result;
    }

    private void LoadPresets(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var pair in obj)
        {
            string name = pair.Key;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (pair.Value is not JsonArray array)
            {
                continue;
            }

            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonNode? element in array)
            {
                string? value = element?.GetValue<string?>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string trimmed = value.Trim();
                if (seen.Add(trimmed))
                {
                    values.Add(trimmed);
                }
            }

            _presets[name.Trim()] = values.Count == 0 ? Array.Empty<string>() : values.ToArray();
        }
    }

    private JsonObject BuildModConfigPathsJson()
    {
        var result = new JsonObject();

        foreach (var pair in _modConfigPaths.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private void LoadModConfigPaths(JsonNode? node)
    {
        _modConfigPaths.Clear();

        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var pair in obj)
        {
            string modId = pair.Key;
            if (string.IsNullOrWhiteSpace(modId))
            {
                continue;
            }

            string? path = pair.Value?.GetValue<string?>();
            string? normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            _modConfigPaths[modId.Trim()] = normalized;
        }
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
