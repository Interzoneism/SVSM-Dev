using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private const string ConfigurationFileName = "VS_ModManager_Config.json";
    private const string LegacyConfigurationFileName = "settings.json";
    private readonly string _configurationPath;
    private readonly string? _legacyConfigurationPath;
    private readonly Dictionary<string, string[]> _presets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AdvancedPresetData> _advancedPresets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _modConfigPaths = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedPresetName;
    private string? _selectedAdvancedPresetName;
    private bool _isAdvancedPresetMode;
    private bool _isCompactView;
    private bool _cacheAllVersionsLocally;
    private string? _modsSortMemberPath;
    private ListSortDirection _modsSortDirection = ListSortDirection.Ascending;

    public UserConfigurationService()
    {
        (_configurationPath, _legacyConfigurationPath) = DetermineConfigurationPaths();
        Load();

        if (!File.Exists(_configurationPath))
        {
            Save();
        }
    }

    public string? DataDirectory { get; private set; }

    public string? GameDirectory { get; private set; }

    public bool IsCompactView => _isCompactView;

    public bool CacheAllVersionsLocally => _cacheAllVersionsLocally;

    public (string? SortMemberPath, ListSortDirection Direction) GetModListSortPreference()
    {
        return (_modsSortMemberPath, _modsSortDirection);
    }

    public string GetConfigurationDirectory()
    {
        string directory = Path.GetDirectoryName(_configurationPath)
            ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string? GetLastSelectedPresetName()
    {
        return _isAdvancedPresetMode ? _selectedAdvancedPresetName : _selectedPresetName;
    }

    public void SetLastSelectedPresetName(string? name)
    {
        string? normalized = NormalizePresetName(name);

        if (_isAdvancedPresetMode)
        {
            if (string.Equals(_selectedAdvancedPresetName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedAdvancedPresetName = normalized;
        }
        else
        {
            if (string.Equals(_selectedPresetName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedPresetName = normalized;
        }

        Save();
    }

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
        if (_isAdvancedPresetMode)
        {
            return _advancedPresets
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ModPreset(pair.Key, pair.Value.DisabledEntries.ToArray(),
                    pair.Value.ModStates.Length == 0 ? Array.Empty<ModPresetModState>() : pair.Value.ModStates.Select(CloneState).ToArray()))
                .ToList();
        }

        return _presets
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ModPreset(pair.Key, pair.Value.ToArray(), Array.Empty<ModPresetModState>()))
            .ToList();
    }

    public bool ContainsPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim();
        return _isAdvancedPresetMode
            ? _advancedPresets.ContainsKey(normalized)
            : _presets.ContainsKey(normalized);
    }

    public bool TryGetPreset(string? name, out ModPreset? preset)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            preset = null;
            return false;
        }

        string normalized = name.Trim();
        if (_isAdvancedPresetMode)
        {
            if (_advancedPresets.TryGetValue(normalized, out AdvancedPresetData? data))
            {
                preset = new ModPreset(
                    normalized,
                    data.DisabledEntries.ToArray(),
                    data.ModStates.Length == 0
                        ? Array.Empty<ModPresetModState>()
                        : data.ModStates.Select(CloneState).ToArray());
                return true;
            }
        }
        else if (_presets.TryGetValue(normalized, out string[]? entries))
        {
            preset = new ModPreset(normalized, entries.ToArray(), Array.Empty<ModPresetModState>());
            return true;
        }

        preset = null;
        return false;
    }

    public void SetPreset(string name, IEnumerable<string> disabledEntries, IEnumerable<ModPresetModState>? modStates = null)
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

        string[] disabled = values.Count == 0 ? Array.Empty<string>() : values.ToArray();

        if (_isAdvancedPresetMode)
        {
            ModPresetModState[] states = BuildSanitizedStates(modStates);
            _advancedPresets[normalized] = new AdvancedPresetData(disabled, states);
        }
        else
        {
            _presets[normalized] = disabled;
        }

        Save();
    }

    public bool RemovePreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim();
        bool removed;
        if (_isAdvancedPresetMode)
        {
            removed = _advancedPresets.Remove(normalized);
            if (removed && string.Equals(_selectedAdvancedPresetName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _selectedAdvancedPresetName = null;
            }
        }
        else
        {
            removed = _presets.Remove(normalized);
            if (removed && string.Equals(_selectedPresetName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _selectedPresetName = null;
            }
        }

        if (!removed)
        {
            return false;
        }

        Save();
        return true;
    }

    public bool IsAdvancedPresetMode => _isAdvancedPresetMode;

    public void SetAdvancedPresetMode(bool isEnabled)
    {
        if (_isAdvancedPresetMode == isEnabled)
        {
            return;
        }

        _isAdvancedPresetMode = isEnabled;
        Save();
    }

    public void SetCompactViewMode(bool isCompact)
    {
        if (_isCompactView == isCompact)
        {
            return;
        }

        _isCompactView = isCompact;
        Save();
    }

    public void SetCacheAllVersionsLocally(bool cacheAllVersionsLocally)
    {
        if (_cacheAllVersionsLocally == cacheAllVersionsLocally)
        {
            return;
        }

        _cacheAllVersionsLocally = cacheAllVersionsLocally;
        Save();
    }

    public void SetModListSortPreference(string? sortMemberPath, ListSortDirection direction)
    {
        string? normalized = string.IsNullOrWhiteSpace(sortMemberPath)
            ? null
            : sortMemberPath.Trim();

        if (string.Equals(_modsSortMemberPath, normalized, StringComparison.Ordinal)
            && _modsSortDirection == direction)
        {
            return;
        }

        _modsSortMemberPath = normalized;
        _modsSortDirection = direction;
        Save();
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
        _advancedPresets.Clear();
        _modConfigPaths.Clear();
        _selectedPresetName = null;
        _selectedAdvancedPresetName = null;

        try
        {
            string? pathToLoad = _configurationPath;
            if (!File.Exists(pathToLoad)
                && !string.IsNullOrWhiteSpace(_legacyConfigurationPath)
                && File.Exists(_legacyConfigurationPath))
            {
                pathToLoad = _legacyConfigurationPath;
            }

            if (string.IsNullOrWhiteSpace(pathToLoad) || !File.Exists(pathToLoad))
            {
                return;
            }

            using FileStream stream = File.OpenRead(pathToLoad);
            JsonNode? node = JsonNode.Parse(stream);
            if (node is not JsonObject obj)
            {
                return;
            }

            bool loadedFromPreferredLocation = string.Equals(
                pathToLoad,
                _configurationPath,
                StringComparison.OrdinalIgnoreCase);

            DataDirectory = NormalizePath(obj["dataDirectory"]?.GetValue<string?>());
            GameDirectory = NormalizePath(obj["gameDirectory"]?.GetValue<string?>());
            _isCompactView = obj["isCompactView"]?.GetValue<bool?>() ?? false;
            _isAdvancedPresetMode = obj["useAdvancedPresets"]?.GetValue<bool?>() ?? false;
            _cacheAllVersionsLocally = obj["cacheAllVersionsLocally"]?.GetValue<bool?>() ?? false;
            _modsSortMemberPath = NormalizeSortMemberPath(obj["modsSortMemberPath"]?.GetValue<string?>());
            _modsSortDirection = ParseSortDirection(obj["modsSortDirection"]?.GetValue<string?>());
            if (loadedFromPreferredLocation)
            {
                LoadClassicPresets(obj["modPresets"]);
                LoadAdvancedPresets(obj["advancedModPresets"]);
            }
            LoadModConfigPaths(obj["modConfigPaths"]);
            _selectedPresetName = NormalizePresetName(obj["selectedPreset"]?.GetValue<string?>());
            _selectedAdvancedPresetName = NormalizePresetName(obj["selectedAdvancedPreset"]?.GetValue<string?>());

            if (!string.IsNullOrWhiteSpace(_selectedPresetName)
                && !_presets.ContainsKey(_selectedPresetName))
            {
                _selectedPresetName = null;
            }

            if (!string.IsNullOrWhiteSpace(_selectedAdvancedPresetName)
                && !_advancedPresets.ContainsKey(_selectedAdvancedPresetName))
            {
                _selectedAdvancedPresetName = null;
            }

            if (!loadedFromPreferredLocation)
            {
                Save();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            DataDirectory = null;
            GameDirectory = null;
            _presets.Clear();
            _advancedPresets.Clear();
            _modConfigPaths.Clear();
            _isAdvancedPresetMode = false;
            _isCompactView = false;
            _cacheAllVersionsLocally = false;
            _modsSortMemberPath = null;
            _modsSortDirection = ListSortDirection.Ascending;
            _selectedPresetName = null;
            _selectedAdvancedPresetName = null;
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
                ["isCompactView"] = _isCompactView,
                ["useAdvancedPresets"] = _isAdvancedPresetMode,
                ["cacheAllVersionsLocally"] = _cacheAllVersionsLocally,
                ["modsSortMemberPath"] = _modsSortMemberPath,
                ["modsSortDirection"] = _modsSortDirection.ToString(),
                ["modPresets"] = BuildClassicPresetsJson(),
                ["advancedModPresets"] = BuildAdvancedPresetsJson(),
                ["modConfigPaths"] = BuildModConfigPathsJson(),
                ["selectedPreset"] = _selectedPresetName,
                ["selectedAdvancedPreset"] = _selectedAdvancedPresetName
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

    private static (string path, string? legacyPath) DetermineConfigurationPaths()
    {
        string preferredDirectory = GetPreferredConfigurationDirectory();
        Directory.CreateDirectory(preferredDirectory);
        string preferredPath = Path.Combine(preferredDirectory, ConfigurationFileName);

        string? legacyDirectory = GetLegacyConfigurationDirectory();
        string? legacyPath = null;

        if (!string.IsNullOrWhiteSpace(legacyDirectory))
        {
            legacyPath = Path.Combine(legacyDirectory!, LegacyConfigurationFileName);
            if (string.Equals(preferredPath, legacyPath, StringComparison.OrdinalIgnoreCase))
            {
                legacyPath = null;
            }
        }

        return (preferredPath, legacyPath);
    }

    private JsonObject BuildClassicPresetsJson()
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

    private JsonObject BuildAdvancedPresetsJson()
    {
        var result = new JsonObject();

        foreach (var pair in _advancedPresets.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var data = pair.Value;
            var preset = new JsonObject
            {
                ["disabledEntries"] = BuildArrayNode(data.DisabledEntries)
            };

            var modsArray = new JsonArray();
            foreach (var state in data.ModStates)
            {
                var modObject = new JsonObject
                {
                    ["modId"] = state.ModId,
                    ["version"] = state.Version,
                    ["isActive"] = state.IsActive
                };

                modsArray.Add(modObject);
            }

            preset["mods"] = modsArray;
            result[pair.Key] = preset;
        }

        return result;
    }

    private void LoadClassicPresets(JsonNode? node)
    {
        _presets.Clear();

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

            string[] entries = ExtractPresetValues(pair.Value);
            _presets[name.Trim()] = entries;
        }
    }

    private void LoadAdvancedPresets(JsonNode? node)
    {
        _advancedPresets.Clear();

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

            if (pair.Value is not JsonObject presetObj)
            {
                continue;
            }

            string[] disabled = ExtractPresetValues(presetObj["disabledEntries"]);
            var states = new List<ModPresetModState>();

            if (presetObj["mods"] is JsonArray modsArray)
            {
                foreach (JsonNode? element in modsArray)
                {
                    if (element is not JsonObject modObj)
                    {
                        continue;
                    }

                    string? modId = modObj["modId"]?.GetValue<string?>();
                    if (string.IsNullOrWhiteSpace(modId))
                    {
                        continue;
                    }

                    string trimmedId = modId.Trim();
                    string? version = modObj["version"]?.GetValue<string?>();
                    bool isActive = modObj["isActive"]?.GetValue<bool?>() ?? true;
                    states.Add(new ModPresetModState(trimmedId, string.IsNullOrWhiteSpace(version) ? null : version, isActive));
                }
            }

            _advancedPresets[name.Trim()] = new AdvancedPresetData(disabled, states.Count == 0 ? Array.Empty<ModPresetModState>() : states.ToArray());
        }
    }

    private static string[] ExtractPresetValues(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return Array.Empty<string>();
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

        return values.Count == 0 ? Array.Empty<string>() : values.ToArray();
    }

    private static ModPresetModState[] BuildSanitizedStates(IEnumerable<ModPresetModState>? states)
    {
        if (states is null)
        {
            return Array.Empty<ModPresetModState>();
        }

        var sanitized = new List<ModPresetModState>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var state in states)
        {
            if (state is null || string.IsNullOrWhiteSpace(state.ModId))
            {
                continue;
            }

            string trimmedId = state.ModId.Trim();
            if (!seen.Add(trimmedId))
            {
                continue;
            }

            sanitized.Add(new ModPresetModState(trimmedId, string.IsNullOrWhiteSpace(state.Version) ? null : state.Version, state.IsActive));
        }

        return sanitized.Count == 0 ? Array.Empty<ModPresetModState>() : sanitized.ToArray();
    }

    private static ModPresetModState CloneState(ModPresetModState state)
    {
        return new ModPresetModState(state.ModId, state.Version, state.IsActive);
    }

    private static JsonArray BuildArrayNode(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(JsonValue.Create(value));
        }

        return array;
    }

    private sealed record AdvancedPresetData(string[] DisabledEntries, ModPresetModState[] ModStates);

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

    private static string GetPreferredConfigurationDirectory()
    {
        string? documents = GetFolder(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return Path.Combine(documents!, "VS Mod Manager");
        }

        string? personal = GetFolder(Environment.SpecialFolder.Personal);
        if (!string.IsNullOrWhiteSpace(personal))
        {
            return Path.Combine(personal!, "VS Mod Manager");
        }

        string? appData = GetFolder(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            return Path.Combine(appData!, "VS Mod Manager");
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home!, ".vs-mod-manager");
        }

        return Path.Combine(AppContext.BaseDirectory, "VS Mod Manager");
    }

    private static string? GetLegacyConfigurationDirectory()
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

    private static string? NormalizeSortMemberPath(string? sortMemberPath)
    {
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return null;
        }

        return sortMemberPath.Trim();
    }

    private static ListSortDirection ParseSortDirection(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out ListSortDirection direction))
        {
            return direction;
        }

        return ListSortDirection.Ascending;
    }

    private static string? NormalizePresetName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
