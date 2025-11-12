using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

/// <summary>
/// Stores simple user configuration values for the mod manager, such as the selected directories.
/// </summary>
public sealed class UserConfigurationService
{
    private static readonly string ConfigurationFileName = DevConfig.ConfigurationFileName;
    private static readonly string ModConfigPathsFileName = DevConfig.ModConfigPathsFileName;
    private static readonly string CurrentModManagerVersion = ResolveCurrentVersion();
    private static readonly string CurrentConfigurationVersion = CurrentModManagerVersion;
    private static readonly IReadOnlyDictionary<string, string> VintageStoryPaletteColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palette.BaseSurface.Shadowed"] = "#FF403529",
            ["Palette.BaseSurface.Raised"] = "#FF4D3D2D",
            ["Palette.BaseSurface.HoverGlow"] = "#FF5A4530",
            ["Palette.Interactive.Surface"] = "#FF453525",
            ["Palette.Accent.Primary"] = "#FF479BBE",
            ["Palette.Interactive.DisabledSurface"] = "#FF332A21",
            ["Palette.Text.Primary"] = "#FFC8BCAE",
            ["Palette.Text.Link"] = "#FF479BBE",
            ["Palette.Bevel.Highlight"] = "#80FFFFFF",
            ["Palette.Bevel.Shadow"] = "#40000000",
            ["Palette.Overlay.HoverTint"] = "#10FFFFFF"
        };

    private static readonly IReadOnlyDictionary<string, string> DarkPaletteColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palette.Accent.Primary"] = "#FF0078D4",
            ["Palette.BaseSurface.Shadowed"] = "#FF202020",
            ["Palette.BaseSurface.HoverGlow"] = "#FF323232",
            ["Palette.BaseSurface.Raised"] = "#FF2B2B2B",
            ["Palette.Bevel.Shadow"] = "#26000000",
            ["Palette.Bevel.Highlight"] = "#21FFFFFF",
            ["Palette.Interactive.DisabledSurface"] = "#FF2A2A2A",
            ["Palette.Interactive.Surface"] = "#FF2E2E2E",
            ["Palette.Overlay.HoverTint"] = "#14FFFFFF",
            ["Palette.Text.Link"] = "#FF0F6CBD",
            ["Palette.Text.Primary"] = "#FFEDEDED"
        };

    private static readonly IReadOnlyDictionary<string, string> LightPaletteColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palette.Accent.Primary"] = "#FF0078D4",
            ["Palette.BaseSurface.HoverGlow"] = "#FFE0EAF5",
            ["Palette.BaseSurface.Raised"] = "#FFFFFFFF",
            ["Palette.BaseSurface.Shadowed"] = "#FFD0DBE5",
            ["Palette.Bevel.Highlight"] = "#80FFFFFF",
            ["Palette.Bevel.Shadow"] = "#66000000",
            ["Palette.Interactive.DisabledSurface"] = "#FFBAC5D0",
            ["Palette.Interactive.Surface"] = "#FFE5F0FA",
            ["Palette.Overlay.HoverTint"] = "#20000000",
            ["Palette.Text.Link"] = "#FF0078D4",
            ["Palette.Text.Primary"] = "#FF000000"
        };
    private static readonly int DefaultModDatabaseSearchResultLimit = DevConfig.DefaultModDatabaseSearchResultLimit;
    private static readonly int DefaultModDatabaseNewModsRecentMonths = DevConfig.DefaultModDatabaseNewModsRecentMonths;
    private static readonly int MaxModDatabaseNewModsRecentMonths = DevConfig.MaxModDatabaseNewModsRecentMonths;
    private static readonly int GameSessionVoteThreshold = DevConfig.GameSessionVoteThreshold;
    private const string DefaultGameProfileName = "Default";
    public const string DefaultProfileName = DefaultGameProfileName;
    private static readonly string[] RedundantRootProfilePropertyNames =
    {
        "dataDirectory",
        "gameDirectory",
        "bulkUpdateModExclusions",
        "skippedModVersions",
        "modConfigPaths",
        "modUsageTracking",
        "customShortcutPath"
    };

    private sealed class GameProfileState
    {
        public GameProfileState(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string? DataDirectory { get; set; }

        public string? GameDirectory { get; set; }

        public string? CustomShortcutPath { get; set; }

        public Dictionary<string, string> ModConfigPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> BulkUpdateModExclusions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> SkippedModVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<ModUsageTrackingKey, int> ModUsageSessionCounts { get; } = new();

        public int LongRunningSessionCount { get; set; }

        public bool HasPendingModUsagePrompt { get; set; }
    }

    private readonly string _configurationPath;
    private readonly string _modConfigPathsPath;
    private readonly Dictionary<string, GameProfileState> _gameProfiles = new(StringComparer.OrdinalIgnoreCase);
    private GameProfileState _activeGameProfile = new(DefaultGameProfileName);
    private readonly Dictionary<string, ModConfigPathEntry> _storedModConfigPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _themePaletteColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _customThemePaletteColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _installedColumnVisibility = new(StringComparer.OrdinalIgnoreCase);
    private string? _previousConfigurationVersion;
    private string? _previousModManagerVersion;
    private string? _selectedPresetName;
    private string _configurationVersion = CurrentConfigurationVersion;
    private string _modManagerVersion = CurrentModManagerVersion;
    private bool _isCompactView;
    private bool _useModDbDesignView = true;
    private ModDatabaseAutoLoadMode _modDatabaseAutoLoadMode = ModDatabaseAutoLoadMode.TotalDownloads;
    private bool _excludeInstalledModDatabaseResults;
    private bool _onlyShowCompatibleModDatabaseResults;
    private bool _cacheAllVersionsLocally = true;
    private bool _disableAutoRefresh;
    private bool _disableAutoRefreshWarningAcknowledged;
    private bool _disableInternetAccess;
    private bool _enableDebugLogging;
    private bool _enableServerOptions;
    private bool _suppressModlistSavePrompt;
    private bool _suppressRefreshCachePrompt;
    private string? _suppressRefreshCachePromptVersion;
    private ModlistAutoLoadBehavior _modlistAutoLoadBehavior = ModlistAutoLoadBehavior.Prompt;
    private int _modDatabaseSearchResultLimit = DefaultModDatabaseSearchResultLimit;
    private int _modDatabaseNewModsRecentMonths = DefaultModDatabaseNewModsRecentMonths;
    private string? _modsSortMemberPath;
    private ListSortDirection _modsSortDirection = ListSortDirection.Ascending;
    private double? _windowWidth;
    private double? _windowHeight;
    private double? _modInfoPanelLeft;
    private double? _modInfoPanelTop;
    private string? _cloudUploaderName;
    private bool _isPersistenceEnabled;
    private bool _hasPendingSave;
    private bool _hasPendingModConfigPathSave;
    private ColorTheme _colorTheme = ColorTheme.VintageStory;
    private bool _hasVersionMismatch;
    private bool _isModUsageTrackingDisabled;
    private bool _migrationCheckCompleted;
    private bool _requireExactVsVersionMatch;
    private bool _firebaseAuthBackupCreated;

    public UserConfigurationService()
    {
        _configurationPath = DetermineConfigurationPath();
        _modConfigPathsPath = DetermineModConfigPathsPath(ModConfigPathsFileName);
        Load();

        _hasPendingSave |= !File.Exists(_configurationPath);
        _hasPendingModConfigPathSave = false;
    }

    public string? DataDirectory => _activeGameProfile.DataDirectory;

    public string? GameDirectory => _activeGameProfile.GameDirectory;

    public string ConfigurationVersion => _configurationVersion;

    public string ModManagerVersion => _modManagerVersion;

    public bool HasVersionMismatch => _hasVersionMismatch;

    public string? PreviousConfigurationVersion => _previousConfigurationVersion;

    public string? PreviousModManagerVersion => _previousModManagerVersion;

    public bool IsCompactView => _isCompactView;

    public bool UseModDbDesignView => _useModDbDesignView;

    public bool CacheAllVersionsLocally => _cacheAllVersionsLocally;

    public ColorTheme ColorTheme => _colorTheme;

    public bool ExcludeInstalledModDatabaseResults => _excludeInstalledModDatabaseResults;

    public bool OnlyShowCompatibleModDatabaseResults => _onlyShowCompatibleModDatabaseResults;

    public bool DisableAutoRefresh => _disableAutoRefresh;

    public bool DisableAutoRefreshWarningAcknowledged => _disableAutoRefreshWarningAcknowledged;

    public bool DisableInternetAccess => _disableInternetAccess;

    public bool EnableDebugLogging => _enableDebugLogging;

    public bool EnableServerOptions => _enableServerOptions;

    public bool SuppressModlistSavePrompt => _suppressModlistSavePrompt;

    public bool SuppressRefreshCachePrompt
    {
        get
        {
            if (!_suppressRefreshCachePrompt)
            {
                return false;
            }

            if (_suppressRefreshCachePromptVersion is null)
            {
                return true;
            }

            return string.Equals(
                _suppressRefreshCachePromptVersion,
                _modManagerVersion,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public ModlistAutoLoadBehavior ModlistAutoLoadBehavior => _modlistAutoLoadBehavior;

    public int ModDatabaseSearchResultLimit => _modDatabaseSearchResultLimit;

    public int ModDatabaseNewModsRecentMonths => _modDatabaseNewModsRecentMonths;

    public ModDatabaseAutoLoadMode ModDatabaseAutoLoadMode => _modDatabaseAutoLoadMode;

    public double? WindowWidth => _windowWidth;

    public double? WindowHeight => _windowHeight;

    public double? ModInfoPanelLeft => _modInfoPanelLeft;

    public double? ModInfoPanelTop => _modInfoPanelTop;

    public string? CustomShortcutPath => _activeGameProfile.CustomShortcutPath;

    public string? CloudUploaderName => _cloudUploaderName;

    public bool HasPendingModUsagePrompt => !_isModUsageTrackingDisabled
        && _activeGameProfile.HasPendingModUsagePrompt
        && _activeGameProfile.ModUsageSessionCounts.Count > 0;

    public bool IsModUsageTrackingEnabled => !_isModUsageTrackingDisabled;

    public string ActiveGameProfileName => _activeGameProfile.Name;

    private GameProfileState ActiveProfile => _activeGameProfile;

    private Dictionary<string, string> ActiveModConfigPaths => ActiveProfile.ModConfigPaths;

    private Dictionary<string, bool> ActiveBulkUpdateModExclusions => ActiveProfile.BulkUpdateModExclusions;

    private Dictionary<string, string> ActiveSkippedModVersions => ActiveProfile.SkippedModVersions;

    private Dictionary<ModUsageTrackingKey, int> ActiveModUsageSessionCounts => ActiveProfile.ModUsageSessionCounts;

    private int ActiveLongRunningSessionCount
    {
        get => ActiveProfile.LongRunningSessionCount;
        set => ActiveProfile.LongRunningSessionCount = value;
    }

    private bool ActiveHasPendingModUsagePrompt
    {
        get => ActiveProfile.HasPendingModUsagePrompt;
        set => ActiveProfile.HasPendingModUsagePrompt = value;
    }

    public IReadOnlyList<string> GetGameProfileNames()
    {
        return _gameProfiles.Keys
            .OrderBy(name => string.Equals(name, DefaultGameProfileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsDefaultGameProfile(string? name)
    {
        string? normalized = NormalizeGameProfileName(name);
        return normalized is not null
            && string.Equals(normalized, DefaultGameProfileName, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryCreateGameProfile(string? name, bool copyFromActive, out string? normalizedName, out string? errorMessage)
    {
        normalizedName = NormalizeGameProfileName(name);

        if (normalizedName is null)
        {
            errorMessage = "Enter a profile name.";
            return false;
        }

        if (string.Equals(normalizedName, DefaultGameProfileName, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "The Default profile already exists.";
            return false;
        }

        if (_gameProfiles.ContainsKey(normalizedName))
        {
            errorMessage = "A profile with that name already exists.";
            return false;
        }

        GameProfileState profile = new(normalizedName);
        _gameProfiles[normalizedName] = profile;

        if (copyFromActive)
        {
            CopyProfileData(ActiveProfile, profile);
        }

        Save();
        errorMessage = null;
        return true;
    }

    public bool TrySetActiveGameProfile(string? name)
    {
        string? normalized = NormalizeGameProfileName(name);
        if (normalized is null)
        {
            return false;
        }

        if (!_gameProfiles.TryGetValue(normalized, out GameProfileState? profile))
        {
            return false;
        }

        if (ReferenceEquals(_activeGameProfile, profile))
        {
            return true;
        }

        _activeGameProfile = profile;
        Save();
        return true;
    }

    public bool TryDeleteGameProfiles(
        IReadOnlyCollection<string> profileNames,
        out string? errorMessage,
        out bool activeProfileChanged)
    {
        activeProfileChanged = false;

        if (profileNames is null || profileNames.Count == 0)
        {
            errorMessage = "Select at least one profile to delete.";
            return false;
        }

        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string profileName in profileNames)
        {
            if (profileName is null)
            {
                continue;
            }

            string? normalized = NormalizeGameProfileName(profileName);
            if (normalized is null)
            {
                continue;
            }

            if (string.Equals(normalized, DefaultGameProfileName, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "The Default profile cannot be deleted.";
                return false;
            }

            normalizedNames.Add(normalized);
        }

        if (normalizedNames.Count == 0)
        {
            errorMessage = "Select at least one profile to delete.";
            return false;
        }

        bool removedAny = false;
        var removedProfiles = new List<string>();

        foreach (string name in normalizedNames)
        {
            if (_gameProfiles.Remove(name))
            {
                removedAny = true;
                removedProfiles.Add(name);
            }
        }

        if (!removedAny)
        {
            errorMessage = "No matching game profiles were found.";
            return false;
        }

        if (normalizedNames.Contains(_activeGameProfile.Name))
        {
            GameProfileState defaultProfile = EnsureProfile(DefaultGameProfileName);
            _activeGameProfile = defaultProfile;
            activeProfileChanged = true;
        }

        Save();
        DeleteProfileBackupDirectories(removedProfiles);
        errorMessage = null;
        return true;
    }

    public string GetActiveGameProfileBackupDirectoryName()
    {
        return BuildBackupDirectoryName(_activeGameProfile.Name);
    }

    private void DeleteProfileBackupDirectories(IEnumerable<string> profileNames)
    {
        if (profileNames is null)
        {
            return;
        }

        string baseDirectory = GetConfigurationDirectory();

        foreach (string profileName in profileNames)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                continue;
            }

            string directoryName = BuildBackupDirectoryName(profileName);
            string directoryPath = Path.Combine(baseDirectory, directoryName);

            TryDeleteDirectory(directoryPath);
        }
    }

    private void CopyProfileData(GameProfileState source, GameProfileState destination)
    {
        destination.DataDirectory = source.DataDirectory;
        destination.GameDirectory = source.GameDirectory;
        destination.CustomShortcutPath = source.CustomShortcutPath;
        destination.ModConfigPaths.Clear();
        foreach (var pair in source.ModConfigPaths)
        {
            destination.ModConfigPaths[pair.Key] = pair.Value;
        }

        destination.BulkUpdateModExclusions.Clear();
        foreach (var pair in source.BulkUpdateModExclusions)
        {
            destination.BulkUpdateModExclusions[pair.Key] = pair.Value;
        }

        destination.SkippedModVersions.Clear();
        foreach (var pair in source.SkippedModVersions)
        {
            destination.SkippedModVersions[pair.Key] = pair.Value;
        }

        destination.ModUsageSessionCounts.Clear();
        foreach (var pair in source.ModUsageSessionCounts)
        {
            destination.ModUsageSessionCounts[pair.Key] = pair.Value;
        }

        destination.LongRunningSessionCount = source.LongRunningSessionCount;
        destination.HasPendingModUsagePrompt = source.HasPendingModUsagePrompt;
    }

    private GameProfileState EnsureProfile(string name)
    {
        if (_gameProfiles.TryGetValue(name, out GameProfileState? profile))
        {
            return profile;
        }

        profile = new GameProfileState(name);
        _gameProfiles[name] = profile;
        return profile;
    }

    private static string? NormalizeGameProfileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string normalized = name.Trim();
        if (normalized.Length > 128)
        {
            normalized = normalized[..128];
        }

        return normalized.Length == 0 ? null : normalized;
    }

    private static string BuildBackupDirectoryName(string profileName)
    {
        string sanitized = new(profileName
            .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = DefaultGameProfileName;
        }

        return $"{DevConfig.BackupDirectoryName}_{sanitized}";
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void LoadGameProfile(GameProfileState profile, JsonObject obj)
    {
        string? dataDirectory = NormalizePath(GetOptionalString(obj["dataDirectory"]));
        if (dataDirectory is not null)
        {
            profile.DataDirectory = dataDirectory;
        }

        string? gameDirectory = NormalizePath(GetOptionalString(obj["gameDirectory"]));
        if (gameDirectory is not null)
        {
            profile.GameDirectory = gameDirectory;
        }

        string? shortcut = NormalizePath(GetOptionalString(obj["customShortcutPath"]));
        if (shortcut is not null)
        {
            profile.CustomShortcutPath = shortcut;
        }

        LoadBulkUpdateModExclusions(obj["bulkUpdateModExclusions"], profile.BulkUpdateModExclusions);
        LoadSkippedModVersions(obj["skippedModVersions"], profile.SkippedModVersions);
        LoadModConfigPaths(obj["modConfigPaths"], profile.ModConfigPaths);
        LoadModUsageTracking(obj["modUsageTracking"], profile);
    }

    private void ApplyLegacyProfileData(JsonObject root, GameProfileState profile)
    {
        string? shortcut = NormalizePath(GetOptionalString(root["customShortcutPath"]));
        if (profile.CustomShortcutPath is null)
        {
            profile.CustomShortcutPath = shortcut;
        }

        if (profile.BulkUpdateModExclusions.Count == 0)
        {
            LoadBulkUpdateModExclusions(root["bulkUpdateModExclusions"], profile.BulkUpdateModExclusions);
        }

        if (profile.SkippedModVersions.Count == 0)
        {
            LoadSkippedModVersions(root["skippedModVersions"], profile.SkippedModVersions);
        }

        if (profile.ModConfigPaths.Count == 0)
        {
            LoadModConfigPaths(root["modConfigPaths"], profile.ModConfigPaths);
        }

        if (profile.ModUsageSessionCounts.Count == 0)
        {
            LoadModUsageTracking(root["modUsageTracking"], profile);
        }
    }

    public bool MigrationCheckCompleted => _migrationCheckCompleted;

    public bool RequireExactVsVersionMatch => _requireExactVsVersionMatch;

    public bool FirebaseAuthBackupCreated => _firebaseAuthBackupCreated;

    public void SetMigrationCheckCompleted()
    {
        if (_migrationCheckCompleted)
        {
            return;
        }

        _migrationCheckCompleted = true;
        Save();
    }

    public void SetFirebaseAuthBackupCreated()
    {
        if (_firebaseAuthBackupCreated)
        {
            return;
        }

        _firebaseAuthBackupCreated = true;
        Save();
    }

    public IReadOnlyDictionary<string, string> GetThemePaletteColors()
    {
        return new Dictionary<string, string>(_themePaletteColors, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<ModUsageTrackingKey, int> GetPendingModUsageCounts()
    {
        if (_isModUsageTrackingDisabled)
        {
            return new Dictionary<ModUsageTrackingKey, int>();
        }

        return new Dictionary<ModUsageTrackingKey, int>(ActiveModUsageSessionCounts);
    }

    public bool RecordLongRunningSession(IReadOnlyList<ModUsageTrackingEntry>? activeMods, out bool recordedUsage)
    {
        recordedUsage = false;

        if (_isModUsageTrackingDisabled)
        {
            return false;
        }

        bool wasPending = HasPendingModUsagePrompt;

        if (activeMods is not null && activeMods.Count > 0)
        {
            foreach (ModUsageTrackingEntry entry in activeMods)
            {
                if (string.IsNullOrEmpty(entry.ModId)
                    || string.IsNullOrEmpty(entry.ModVersion)
                    || string.IsNullOrEmpty(entry.GameVersion))
                {
                    continue;
                }

                if (!entry.CanSubmitVote || entry.HasUserVote)
                {
                    continue;
                }

                var key = new ModUsageTrackingKey(entry.ModId, entry.ModVersion, entry.GameVersion);
                if (ActiveModUsageSessionCounts.TryGetValue(key, out int existing))
                {
                    ActiveModUsageSessionCounts[key] = existing >= int.MaxValue - 1 ? int.MaxValue : existing + 1;
                }
                else
                {
                    ActiveModUsageSessionCounts[key] = 1;
                }

                recordedUsage = true;
            }
        }

        if (ActiveLongRunningSessionCount < int.MaxValue)
        {
            ActiveLongRunningSessionCount++;
        }

        if (ActiveLongRunningSessionCount > GameSessionVoteThreshold)
        {
            ActiveLongRunningSessionCount = GameSessionVoteThreshold;
        }

        if (ActiveLongRunningSessionCount >= GameSessionVoteThreshold && ActiveModUsageSessionCounts.Count > 0)
        {
            ActiveHasPendingModUsagePrompt = true;
        }

        Save();

        bool isPending = HasPendingModUsagePrompt;
        return !wasPending && isPending;
    }

    public void DisableModUsageTracking()
    {
        if (_isModUsageTrackingDisabled)
        {
            return;
        }

        _isModUsageTrackingDisabled = true;
        bool hadTrackingState = ActiveLongRunningSessionCount != 0
            || ActiveModUsageSessionCounts.Count > 0
            || ActiveHasPendingModUsagePrompt;
        ResetModUsageTracking();
        if (!hadTrackingState)
        {
            Save();
        }
    }

    public void ResetModUsageCounts(IEnumerable<ModUsageTrackingKey>? keys)
    {
        if (keys is null)
        {
            return;
        }

        bool changed = false;

        foreach (ModUsageTrackingKey key in keys)
        {
            if (!key.IsValid)
            {
                continue;
            }

            if (ActiveModUsageSessionCounts.Remove(key))
            {
                changed = true;
            }
        }

        if (ActiveModUsageSessionCounts.Count == 0)
        {
            if (ActiveLongRunningSessionCount != 0 || ActiveHasPendingModUsagePrompt)
            {
                ActiveLongRunningSessionCount = 0;
                ActiveHasPendingModUsagePrompt = false;
                changed = true;
            }
        }
        else
        {
            bool shouldPrompt = ActiveLongRunningSessionCount >= GameSessionVoteThreshold;
            if (ActiveHasPendingModUsagePrompt != shouldPrompt)
            {
                ActiveHasPendingModUsagePrompt = shouldPrompt;
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public void CompleteModUsageVotes(IEnumerable<ModUsageTrackingKey>? completedKeys)
    {
        bool changed = false;

        if (completedKeys is not null)
        {
            foreach (ModUsageTrackingKey key in completedKeys)
            {
                if (!key.IsValid)
                {
                    continue;
                }

                if (ActiveModUsageSessionCounts.Remove(key))
                {
                    changed = true;
                }
            }
        }

        if (ActiveModUsageSessionCounts.Count == 0)
        {
            if (ActiveLongRunningSessionCount != 0 || ActiveHasPendingModUsagePrompt)
            {
                ActiveLongRunningSessionCount = 0;
                ActiveHasPendingModUsagePrompt = false;
                changed = true;
            }
        }
        else
        {
            ActiveHasPendingModUsagePrompt = true;
            if (ActiveLongRunningSessionCount < GameSessionVoteThreshold)
            {
                ActiveLongRunningSessionCount = GameSessionVoteThreshold;
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public void ResetModUsageTracking()
    {
        if (ActiveLongRunningSessionCount == 0 && ActiveModUsageSessionCounts.Count == 0 && !ActiveHasPendingModUsagePrompt)
        {
            return;
        }

        ActiveLongRunningSessionCount = 0;
        ActiveModUsageSessionCounts.Clear();
        ActiveHasPendingModUsagePrompt = false;
        Save();
    }

    public bool TrySetThemePaletteColor(string key, string color)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string normalizedKey = key.Trim();
        if (!_themePaletteColors.ContainsKey(normalizedKey))
        {
            return false;
        }

        if (!TryNormalizeHexColor(color, out string normalizedColor))
        {
            return false;
        }

        if (_themePaletteColors.TryGetValue(normalizedKey, out string? current)
            && string.Equals(current, normalizedColor, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _themePaletteColors[normalizedKey] = normalizedColor;
        if (_colorTheme == ColorTheme.Custom)
        {
            EnsureCustomThemePaletteInitialized();
            _customThemePaletteColors[normalizedKey] = normalizedColor;
        }
        Save();
        return true;
    }

    public void ResetThemePalette()
    {
        if (_colorTheme == ColorTheme.Custom)
        {
            ResetCustomThemePaletteToDefaults();
        }

        ResetThemePaletteToDefaults();
        Save();
    }

    public static IReadOnlyDictionary<string, string> GetDefaultThemePalette(ColorTheme theme)
    {
        return new Dictionary<string, string>(GetDefaultPalette(theme), StringComparer.OrdinalIgnoreCase);
    }

    public (string? SortMemberPath, ListSortDirection Direction) GetModListSortPreference()
    {
        return (_modsSortMemberPath, _modsSortDirection);
    }

    public bool? GetInstalledColumnVisibility(string columnName)
    {
        string? normalized = NormalizeInstalledColumnKey(columnName);

        if (normalized is null)
        {
            return null;
        }

        return _installedColumnVisibility.TryGetValue(normalized, out bool value)
            ? value
            : null;
    }

    public void SetInstalledColumnVisibility(string columnName, bool isVisible)
    {
        string? normalized = NormalizeInstalledColumnKey(columnName);

        if (normalized is null)
        {
            throw new ArgumentException("Column name cannot be empty.", nameof(columnName));
        }

        if (_installedColumnVisibility.TryGetValue(normalized, out bool current)
            && current == isVisible)
        {
            return;
        }

        _installedColumnVisibility[normalized] = isVisible;
        Save();
    }

    public string GetConfigurationDirectory()
    {
        string directory = Path.GetDirectoryName(_configurationPath)
            ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);
        return directory;
    }

    public void EnablePersistence()
    {
        if (_isPersistenceEnabled)
        {
            return;
        }

        _isPersistenceEnabled = true;

        if (_hasPendingSave)
        {
            PersistConfiguration();
        }

        if (_hasPendingModConfigPathSave)
        {
            PersistModConfigPathHistory();
        }
    }

    public string? GetLastSelectedPresetName() => _selectedPresetName;

    public void SetLastSelectedPresetName(string? name)
    {
        string? normalized = NormalizePresetName(name);

        if (string.Equals(_selectedPresetName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedPresetName = normalized;

        Save();
    }

    public bool TryGetModConfigPath(string? modId, out string? path)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            path = null;
            return false;
        }

        string key = modId.Trim();
        if (ActiveModConfigPaths.TryGetValue(key, out path))
        {
            return true;
        }

        if (_storedModConfigPaths.TryGetValue(key, out ModConfigPathEntry? entry) && entry is not null)
        {
            string? combined = BuildFullConfigPath(entry);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                string combinedPath = combined!;
                ActiveModConfigPaths[key] = combinedPath;
                path = combinedPath;
                return true;
            }
        }

        path = null;
        return false;
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

        string key = modId.Trim();
        ActiveModConfigPaths[key] = normalized;
        UpdatePersistentModConfigPath(key, normalized);
        Save();
    }

    public void RemoveModConfigPath(string? modId, bool preserveHistory = false)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        string key = modId.Trim();
        bool removed = ActiveModConfigPaths.Remove(key);
        bool historyChanged = false;

        if (!preserveHistory)
        {
            historyChanged = _storedModConfigPaths.Remove(key);
        }

        if (removed)
        {
            Save();
        }

        if (historyChanged)
        {
            SaveModConfigPathHistory();
        }
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

    public void SetModDbDesignViewMode(bool useModDbDesignView)
    {
        if (_useModDbDesignView == useModDbDesignView)
        {
            return;
        }

        _useModDbDesignView = useModDbDesignView;
        Save();
    }

    public void SetColorTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverride = null)
    {
        bool paletteChanged = false;

        if (_colorTheme != theme)
        {
            _colorTheme = theme;
            ResetThemePaletteToDefaults();
            paletteChanged = true;
        }

        if (paletteOverride is not null)
        {
            paletteChanged |= ApplyThemePaletteOverride(paletteOverride);
        }

        if (paletteChanged)
        {
            Save();
        }
    }

    public void SetEnableDebugLogging(bool enableDebugLogging)
    {
        if (_enableDebugLogging == enableDebugLogging)
        {
            return;
        }

        _enableDebugLogging = enableDebugLogging;
        Save();
    }

    public void SetEnableServerOptions(bool enableServerOptions)
    {
        if (_enableServerOptions == enableServerOptions)
        {
            return;
        }

        _enableServerOptions = enableServerOptions;
        Save();
    }

    public void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior behavior)
    {
        if (_modlistAutoLoadBehavior == behavior)
        {
            return;
        }

        _modlistAutoLoadBehavior = behavior;
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

    public void SetModDatabaseAutoLoadMode(ModDatabaseAutoLoadMode mode)
    {
        if (_modDatabaseAutoLoadMode == mode)
        {
            return;
        }

        _modDatabaseAutoLoadMode = mode;
        Save();
    }

    public void SetModDatabaseSearchResultLimit(int limit)
    {
        int normalized = NormalizeModDatabaseSearchResultLimit(limit);

        if (_modDatabaseSearchResultLimit == normalized)
        {
            return;
        }

        _modDatabaseSearchResultLimit = normalized;
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

    public void SetWindowDimensions(double width, double height)
    {
        double? normalizedWidth = NormalizeWindowDimension(width);
        double? normalizedHeight = NormalizeWindowDimension(height);

        if (!normalizedWidth.HasValue || !normalizedHeight.HasValue)
        {
            return;
        }

        bool hasWidthChanged = !_windowWidth.HasValue || Math.Abs(_windowWidth.Value - normalizedWidth.Value) > 0.1;
        bool hasHeightChanged = !_windowHeight.HasValue || Math.Abs(_windowHeight.Value - normalizedHeight.Value) > 0.1;

        if (!hasWidthChanged && !hasHeightChanged)
        {
            return;
        }

        _windowWidth = normalizedWidth;
        _windowHeight = normalizedHeight;
        Save();
    }

    public void SetModInfoPanelPosition(double left, double top)
    {
        double? normalizedLeft = NormalizeModInfoCoordinate(left);
        double? normalizedTop = NormalizeModInfoCoordinate(top);

        if (!normalizedLeft.HasValue || !normalizedTop.HasValue)
        {
            return;
        }

        bool hasLeftChanged = !_modInfoPanelLeft.HasValue || Math.Abs(_modInfoPanelLeft.Value - normalizedLeft.Value) > 0.1;
        bool hasTopChanged = !_modInfoPanelTop.HasValue || Math.Abs(_modInfoPanelTop.Value - normalizedTop.Value) > 0.1;

        if (!hasLeftChanged && !hasTopChanged)
        {
            return;
        }

        _modInfoPanelLeft = normalizedLeft;
        _modInfoPanelTop = normalizedTop;
        Save();
    }

    public void SetDataDirectory(string path)
    {
        _activeGameProfile.DataDirectory = NormalizePath(path);
        Save();
    }

    public void ClearDataDirectory()
    {
        if (_activeGameProfile.DataDirectory is null)
        {
            return;
        }

        _activeGameProfile.DataDirectory = null;
        Save();
    }

    public void SetGameDirectory(string path)
    {
        _activeGameProfile.GameDirectory = NormalizePath(path);
        Save();
    }

    public void ClearGameDirectory()
    {
        if (_activeGameProfile.GameDirectory is null)
        {
            return;
        }

        _activeGameProfile.GameDirectory = null;
        Save();
    }

    public void SetCustomShortcutPath(string path)
    {
        string? normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("The shortcut path is invalid.", nameof(path));
        }

        if (string.Equals(ActiveProfile.CustomShortcutPath, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ActiveProfile.CustomShortcutPath = normalized;
        Save();
    }

    public void ClearCustomShortcutPath()
    {
        if (ActiveProfile.CustomShortcutPath is null)
        {
            return;
        }

        ActiveProfile.CustomShortcutPath = null;
        Save();
    }

    public void SetDisableAutoRefresh(bool disable)
    {
        if (_disableAutoRefresh == disable)
        {
            return;
        }

        _disableAutoRefresh = disable;
        Save();
    }

    public void SetDisableAutoRefreshWarningAcknowledged(bool acknowledged)
    {
        if (_disableAutoRefreshWarningAcknowledged == acknowledged)
        {
            return;
        }

        _disableAutoRefreshWarningAcknowledged = acknowledged;
        Save();
    }

    public void SetDisableInternetAccess(bool disable)
    {
        if (_disableInternetAccess == disable)
        {
            return;
        }

        _disableInternetAccess = disable;
        Save();
    }

    public void SetSuppressModlistSavePrompt(bool suppress)
    {
        if (_suppressModlistSavePrompt == suppress)
        {
            return;
        }

        _suppressModlistSavePrompt = suppress;
        Save();
    }

    public void SetSuppressRefreshCachePrompt(bool suppress)
    {
        string? normalizedVersion = suppress
            ? NormalizeVersion(_modManagerVersion) ?? _modManagerVersion
            : null;

        if (_suppressRefreshCachePrompt == suppress
            && string.Equals(
                _suppressRefreshCachePromptVersion,
                normalizedVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _suppressRefreshCachePrompt = suppress;
        _suppressRefreshCachePromptVersion = normalizedVersion;
        Save();
    }

    public void SetCloudUploaderName(string? name)
    {
        string? normalized = NormalizeUploaderName(name);

        if (string.Equals(_cloudUploaderName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _cloudUploaderName = normalized;
        Save();
    }

    public void SetExcludeInstalledModDatabaseResults(bool exclude)
    {
        if (_excludeInstalledModDatabaseResults == exclude)
        {
            return;
        }

        _excludeInstalledModDatabaseResults = exclude;
        Save();
    }

    public void SetOnlyShowCompatibleModDatabaseResults(bool onlyCompatible)
    {
        if (_onlyShowCompatibleModDatabaseResults == onlyCompatible)
        {
            return;
        }

        _onlyShowCompatibleModDatabaseResults = onlyCompatible;
        Save();
    }

    public void SetRequireExactVsVersionMatch(bool requireExact)
    {
        if (_requireExactVsVersionMatch == requireExact)
        {
            return;
        }

        _requireExactVsVersionMatch = requireExact;
        Save();
    }

    public bool IsModExcludedFromBulkUpdates(string? modId)
    {
        string? normalized = NormalizeModId(modId);
        if (normalized is null)
        {
            return false;
        }

        return ActiveBulkUpdateModExclusions.TryGetValue(normalized, out bool isExcluded) && isExcluded;
    }

    public void SetModExcludedFromBulkUpdates(string? modId, bool isExcluded)
    {
        string? normalized = NormalizeModId(modId);
        if (normalized is null)
        {
            return;
        }

        if (isExcluded)
        {
            if (ActiveBulkUpdateModExclusions.TryGetValue(normalized, out bool current) && current)
            {
                return;
            }

            ActiveBulkUpdateModExclusions[normalized] = true;
            Save();
            return;
        }

        if (!ActiveBulkUpdateModExclusions.Remove(normalized))
        {
            return;
        }

        Save();
    }

    public void SkipModVersion(string? modId, string? version)
    {
        string? normalizedId = NormalizeModId(modId);
        if (normalizedId is null)
        {
            return;
        }

        string? normalizedVersion = NormalizeVersion(version);
        if (normalizedVersion is null)
        {
            return;
        }

        if (ActiveSkippedModVersions.TryGetValue(normalizedId, out string? current)
            && string.Equals(current, normalizedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ActiveSkippedModVersions[normalizedId] = normalizedVersion;
        Save();
    }

    public bool ShouldSkipModVersion(string? modId, string? version)
    {
        string? normalizedId = NormalizeModId(modId);
        if (normalizedId is null)
        {
            return false;
        }

        if (!ActiveSkippedModVersions.TryGetValue(normalizedId, out string? storedVersion))
        {
            return false;
        }

        string? normalizedVersion = NormalizeVersion(version);
        if (normalizedVersion is null
            || !string.Equals(storedVersion, normalizedVersion, StringComparison.OrdinalIgnoreCase))
        {
            if (ActiveSkippedModVersions.Remove(normalizedId))
            {
                Save();
            }

        }

        return string.Equals(storedVersion, normalizedVersion, StringComparison.OrdinalIgnoreCase);
    }


    private void Load()
    {
        _gameProfiles.Clear();
        GameProfileState defaultProfile = new(DefaultGameProfileName);
        _gameProfiles[defaultProfile.Name] = defaultProfile;
        _activeGameProfile = defaultProfile;
        ResetCustomThemePaletteToDefaults();
        _colorTheme = ColorTheme.VintageStory;
        ResetThemePaletteToDefaults();
        _selectedPresetName = null;
    
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
    
            string? originalConfigurationVersion = NormalizeVersion(GetOptionalString(obj["configurationVersion"]));
            string? originalModManagerVersion = NormalizeVersion(GetOptionalString(obj["modManagerVersion"]));
            InitializeVersionMetadata(originalConfigurationVersion, originalModManagerVersion);
    
            string? rootDataDirectory = NormalizePath(GetOptionalString(obj["dataDirectory"]));
            string? rootGameDirectory = NormalizePath(GetOptionalString(obj["gameDirectory"]));
            defaultProfile.DataDirectory = rootDataDirectory;
            defaultProfile.GameDirectory = rootGameDirectory;
    
            _isCompactView = obj["isCompactView"]?.GetValue<bool?>() ?? false;
            _useModDbDesignView = obj["useModDbDesignView"]?.GetValue<bool?>() ?? true;
            _cacheAllVersionsLocally = obj["cacheAllVersionsLocally"]?.GetValue<bool?>() ?? true;
            _disableAutoRefresh = obj["disableAutoRefresh"]?.GetValue<bool?>() ?? false;
            _disableAutoRefreshWarningAcknowledged =
                obj["disableAutoRefreshWarningAcknowledged"]?.GetValue<bool?>() ?? false;
            _disableInternetAccess = obj["disableInternetAccess"]?.GetValue<bool?>() ?? false;
            _isModUsageTrackingDisabled = obj["modUsageTrackingDisabled"]?.GetValue<bool?>() ?? false;
            _enableDebugLogging = obj["enableDebugLogging"]?.GetValue<bool?>() ?? false;
            _enableServerOptions = obj["enableServerOptions"]?.GetValue<bool?>() ?? false;
            _suppressModlistSavePrompt = obj["suppressModlistSavePrompt"]?.GetValue<bool?>() ?? false;
            _suppressRefreshCachePrompt = obj["suppressRefreshCachePrompt"]?.GetValue<bool?>() ?? false;
            _suppressRefreshCachePromptVersion = NormalizeVersion(
                GetOptionalString(obj["suppressRefreshCachePromptVersion"]));
            if (_suppressRefreshCachePrompt)
            {
                if (_suppressRefreshCachePromptVersion is null)
                {
                    _suppressRefreshCachePromptVersion = _previousModManagerVersion ?? _modManagerVersion;
                    _hasPendingSave = true;
                }
            }
            else
            {
                _suppressRefreshCachePromptVersion = null;
            }
    
            string? colorThemeValue = GetOptionalString(obj["colorTheme"]);
            bool? legacyDarkVsMode = obj["useDarkVsMode"]?.GetValue<bool?>();
            if (!string.IsNullOrWhiteSpace(colorThemeValue)
                && Enum.TryParse(colorThemeValue.Trim(), ignoreCase: true, out ColorTheme parsedTheme))
            {
                _colorTheme = parsedTheme;
            }
            else
            {
                _colorTheme = legacyDarkVsMode.HasValue && !legacyDarkVsMode.Value
                    ? ColorTheme.Light
                    : ColorTheme.VintageStory;
                _hasPendingSave = true;
            }
    
            bool hasCustomPalette = LoadCustomThemePalette(obj["customThemePalette"]);
            ResetThemePaletteToDefaults();
            _modlistAutoLoadBehavior = ParseModlistAutoLoadBehavior(GetOptionalString(obj["modlistAutoLoadBehavior"]));
            _modsSortMemberPath = NormalizeSortMemberPath(GetOptionalString(obj["modsSortMemberPath"]));
            _modsSortDirection = ParseSortDirection(GetOptionalString(obj["modsSortDirection"]));
            _modDatabaseSearchResultLimit = NormalizeModDatabaseSearchResultLimit(obj["modDatabaseSearchResultLimit"]?.GetValue<int?>());
            _modDatabaseNewModsRecentMonths = NormalizeModDatabaseNewModsRecentMonths(
                obj["modDatabaseNewModsRecentMonths"]?.GetValue<int?>());
            _modDatabaseAutoLoadMode = ParseModDatabaseAutoLoadMode(GetOptionalString(obj["modDatabaseAutoLoadMode"]));
            _excludeInstalledModDatabaseResults = obj["excludeInstalledModDatabaseResults"]?.GetValue<bool?>() ?? false;
            _onlyShowCompatibleModDatabaseResults = obj["onlyShowCompatibleModDatabaseResults"]?.GetValue<bool?>() ?? false;
            _requireExactVsVersionMatch = obj["requireExactVsVersionMatch"]?.GetValue<bool?>() ?? false;
            _windowWidth = NormalizeWindowDimension(obj["windowWidth"]?.GetValue<double?>());
            _windowHeight = NormalizeWindowDimension(obj["windowHeight"]?.GetValue<double?>());
            _modInfoPanelLeft = NormalizeModInfoCoordinate(obj["modInfoPanelLeft"]?.GetValue<double?>());
            _modInfoPanelTop = NormalizeModInfoCoordinate(obj["modInfoPanelTop"]?.GetValue<double?>());
    
            LoadInstalledColumnVisibility(obj["installedColumnVisibility"]);
            LoadThemePalette(obj["themePalette"] ?? obj["darkVsPalette"]);
            if (_colorTheme == ColorTheme.Custom)
            {
                SyncCustomThemePaletteWithCurrentTheme();
                if (!hasCustomPalette)
                {
                    _hasPendingSave = true;
                }
            }
    
            _selectedPresetName = NormalizePresetName(GetOptionalString(obj["selectedPreset"]));
            _cloudUploaderName = NormalizeUploaderName(GetOptionalString(obj["cloudUploaderName"]));
            _migrationCheckCompleted = obj["migrationCheckCompleted"]?.GetValue<bool?>() ?? false;
            _firebaseAuthBackupCreated = obj["firebaseAuthBackupCreated"]?.GetValue<bool?>() ?? false;
    
            bool profilesFound = false;
            if (obj["gameProfiles"] is JsonObject profilesObj)
            {
                profilesFound = true;
                foreach ((string key, JsonNode? value) in profilesObj)
                {
                    string? normalizedName = NormalizeGameProfileName(key);
                    if (normalizedName is null || value is not JsonObject profileObj)
                    {
                        continue;
                    }
    
                    GameProfileState profile = EnsureProfile(normalizedName);
                    LoadGameProfile(profile, profileObj);
                }
            }
    
            if (!profilesFound)
            {
                ApplyLegacyProfileData(obj, defaultProfile);
            }
            else
            {
                if (!_gameProfiles.TryGetValue(DefaultGameProfileName, out GameProfileState? existingDefault)
                    || existingDefault is null)
                {
                    defaultProfile = new GameProfileState(DefaultGameProfileName);
                    _gameProfiles[defaultProfile.Name] = defaultProfile;
                }
                else
                {
                    defaultProfile = existingDefault;
                }
    
                defaultProfile.DataDirectory ??= rootDataDirectory;
                defaultProfile.GameDirectory ??= rootGameDirectory;
                ApplyLegacyProfileData(obj, defaultProfile);
            }
    
            string? rootShortcut = NormalizePath(GetOptionalString(obj["customShortcutPath"]));
            foreach (GameProfileState profile in _gameProfiles.Values)
            {
                profile.DataDirectory ??= rootDataDirectory;
                profile.GameDirectory ??= rootGameDirectory;
                if (profile.CustomShortcutPath is null)
                {
                    profile.CustomShortcutPath = rootShortcut;
                }
            }
    
            string? activeName = NormalizeGameProfileName(GetOptionalString(obj["activeGameProfile"]));
            if (activeName is not null && _gameProfiles.TryGetValue(activeName, out GameProfileState? activeProfile))
            {
                _activeGameProfile = activeProfile;
            }
            else
            {
                _activeGameProfile = defaultProfile;
            }
    
            if (_isModUsageTrackingDisabled)
            {
                foreach (GameProfileState profile in _gameProfiles.Values)
                {
                    profile.ModUsageSessionCounts.Clear();
                    profile.LongRunningSessionCount = 0;
                    profile.HasPendingModUsagePrompt = false;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _gameProfiles.Clear();
            GameProfileState resetProfile = new(DefaultGameProfileName);
            _gameProfiles[resetProfile.Name] = resetProfile;
            _activeGameProfile = resetProfile;
            _colorTheme = ColorTheme.VintageStory;
            ResetCustomThemePaletteToDefaults();
            ResetThemePaletteToDefaults();
            _configurationVersion = CurrentConfigurationVersion;
            _modManagerVersion = CurrentModManagerVersion;
            _isCompactView = false;
            _useModDbDesignView = true;
            _cacheAllVersionsLocally = true;
            _disableAutoRefresh = false;
            _disableAutoRefreshWarningAcknowledged = false;
            _disableInternetAccess = false;
            _enableDebugLogging = false;
            _enableServerOptions = false;
            _suppressModlistSavePrompt = false;
            _suppressRefreshCachePrompt = false;
            _suppressRefreshCachePromptVersion = null;
            _modlistAutoLoadBehavior = ModlistAutoLoadBehavior.Prompt;
            _modsSortMemberPath = null;
            _modsSortDirection = ListSortDirection.Ascending;
            _selectedPresetName = null;
            _modDatabaseSearchResultLimit = DefaultModDatabaseSearchResultLimit;
            _modDatabaseNewModsRecentMonths = DefaultModDatabaseNewModsRecentMonths;
            _modDatabaseAutoLoadMode = ModDatabaseAutoLoadMode.TotalDownloads;
            _excludeInstalledModDatabaseResults = false;
            _onlyShowCompatibleModDatabaseResults = false;
            _requireExactVsVersionMatch = false;
            _windowWidth = null;
            _windowHeight = null;
            _modInfoPanelLeft = null;
            _modInfoPanelTop = null;
            _cloudUploaderName = null;
            _installedColumnVisibility.Clear();
            _isModUsageTrackingDisabled = false;
            _migrationCheckCompleted = false;
            _firebaseAuthBackupCreated = false;
        }
    
        LoadPersistentModConfigPaths();
    }

    private void Save()
    {
        _hasPendingSave = true;

        if (!_isPersistenceEnabled)
        {
            return;
        }

        PersistConfiguration();
    }

    private void PersistConfiguration()
    {
        try
        {
            string directory = Path.GetDirectoryName(_configurationPath)!;
            Directory.CreateDirectory(directory);

            var obj = new JsonObject
            {
                ["configurationVersion"] = _configurationVersion,
                ["modManagerVersion"] = _modManagerVersion,
                ["dataDirectory"] = DataDirectory,
                ["gameDirectory"] = GameDirectory,
                ["activeGameProfile"] = _activeGameProfile.Name,
                ["isCompactView"] = _isCompactView,
                ["useModDbDesignView"] = _useModDbDesignView,
                ["cacheAllVersionsLocally"] = _cacheAllVersionsLocally,
                ["disableAutoRefresh"] = _disableAutoRefresh,
                ["disableAutoRefreshWarningAcknowledged"] = _disableAutoRefreshWarningAcknowledged,
                ["disableInternetAccess"] = _disableInternetAccess,
                ["modUsageTrackingDisabled"] = _isModUsageTrackingDisabled,
                ["enableDebugLogging"] = _enableDebugLogging,
                ["enableServerOptions"] = _enableServerOptions,
                ["suppressModlistSavePrompt"] = _suppressModlistSavePrompt,
                ["suppressRefreshCachePrompt"] = _suppressRefreshCachePrompt,
                ["suppressRefreshCachePromptVersion"] = _suppressRefreshCachePromptVersion,
                ["useDarkVsMode"] = _colorTheme != ColorTheme.Light,
                ["colorTheme"] = _colorTheme.ToString(),
                ["modlistAutoLoadBehavior"] = _modlistAutoLoadBehavior.ToString(),
                ["modsSortMemberPath"] = _modsSortMemberPath,
                ["modsSortDirection"] = _modsSortDirection.ToString(),
                ["modDatabaseSearchResultLimit"] = _modDatabaseSearchResultLimit,
                ["modDatabaseNewModsRecentMonths"] = _modDatabaseNewModsRecentMonths,
                ["modDatabaseAutoLoadMode"] = _modDatabaseAutoLoadMode.ToString(),
                ["excludeInstalledModDatabaseResults"] = _excludeInstalledModDatabaseResults,
                ["onlyShowCompatibleModDatabaseResults"] = _onlyShowCompatibleModDatabaseResults,
                ["requireExactVsVersionMatch"] = _requireExactVsVersionMatch,
                ["windowWidth"] = _windowWidth,
                ["windowHeight"] = _windowHeight,
                ["modInfoPanelLeft"] = _modInfoPanelLeft,
                ["modInfoPanelTop"] = _modInfoPanelTop,
                ["bulkUpdateModExclusions"] = BuildBulkUpdateModExclusionsJson(_activeGameProfile.BulkUpdateModExclusions),
                ["skippedModVersions"] = BuildSkippedModVersionsJson(_activeGameProfile.SkippedModVersions),
                ["modConfigPaths"] = BuildModConfigPathsJson(_activeGameProfile.ModConfigPaths),
                ["installedColumnVisibility"] = BuildInstalledColumnVisibilityJson(),
                ["modUsageTracking"] = BuildModUsageTrackingJson(_activeGameProfile),
                ["themePalette"] = BuildThemePaletteJson(),
                ["darkVsPalette"] = BuildThemePaletteJson(),
                ["customThemePalette"] = BuildCustomThemePaletteJson(),
                ["selectedPreset"] = _selectedPresetName,
                ["customShortcutPath"] = _activeGameProfile.CustomShortcutPath,
                ["cloudUploaderName"] = _cloudUploaderName,
                ["migrationCheckCompleted"] = _migrationCheckCompleted,
                ["firebaseAuthBackupCreated"] = _firebaseAuthBackupCreated,
                ["gameProfiles"] = BuildGameProfilesJson()
            };

            RemoveRedundantRootProfileValues(obj);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(_configurationPath, obj.ToJsonString(options));
            _hasPendingSave = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the configuration is a best-effort attempt. Ignore failures silently.
        }
    }

    private void RemoveRedundantRootProfileValues(JsonObject root)
    {
        if (root["gameProfiles"] is not JsonObject profiles
            || !profiles.ContainsKey(DefaultGameProfileName))
        {
            return;
        }

        foreach (string propertyName in RedundantRootProfilePropertyNames)
        {
            root.Remove(propertyName);
        }
    }

    private void InitializeVersionMetadata(string? configurationVersion, string? modManagerVersion)
    {
        bool requiresSave = false;

        _previousConfigurationVersion = configurationVersion;
        _previousModManagerVersion = modManagerVersion;

        bool configurationMismatch = !string.IsNullOrWhiteSpace(configurationVersion)
            && !string.Equals(configurationVersion, CurrentConfigurationVersion, StringComparison.OrdinalIgnoreCase);

        bool modManagerMismatch = !string.IsNullOrWhiteSpace(modManagerVersion)
            && !string.Equals(modManagerVersion, CurrentModManagerVersion, StringComparison.OrdinalIgnoreCase);

        _hasVersionMismatch = configurationMismatch || modManagerMismatch;

        string resolvedConfigurationVersion = string.IsNullOrWhiteSpace(configurationVersion)
            ? CurrentConfigurationVersion
            : configurationVersion!;

        string resolvedModManagerVersion = string.IsNullOrWhiteSpace(modManagerVersion)
            ? CurrentModManagerVersion
            : modManagerVersion!;

        if (!string.Equals(resolvedConfigurationVersion, CurrentConfigurationVersion, StringComparison.OrdinalIgnoreCase))
        {
            resolvedConfigurationVersion = CurrentConfigurationVersion;
            requiresSave = true;
        }

        if (!string.Equals(resolvedModManagerVersion, CurrentModManagerVersion, StringComparison.OrdinalIgnoreCase))
        {
            resolvedModManagerVersion = CurrentModManagerVersion;
            requiresSave = true;
        }

        _configurationVersion = resolvedConfigurationVersion;
        _modManagerVersion = resolvedModManagerVersion;

        if (requiresSave)
        {
            _hasPendingSave = true;
        }
    }

    private static int NormalizeModDatabaseSearchResultLimit(int? value)
    {
        if (!value.HasValue)
        {
            return DefaultModDatabaseSearchResultLimit;
        }

        int normalized = value.Value;
        if (normalized <= 0)
        {
            return DefaultModDatabaseSearchResultLimit;
        }

        return Math.Max(normalized, 1);
    }

    private static int NormalizeModDatabaseNewModsRecentMonths(int? value)
    {
        if (!value.HasValue)
        {
            return DefaultModDatabaseNewModsRecentMonths;
        }

        int normalized = value.Value;
        if (normalized <= 0)
        {
            return DefaultModDatabaseNewModsRecentMonths;
        }

        return Math.Clamp(normalized, 1, MaxModDatabaseNewModsRecentMonths);
    }

    private static ModlistAutoLoadBehavior ParseModlistAutoLoadBehavior(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out ModlistAutoLoadBehavior behavior))
        {
            return behavior;
        }

        return ModlistAutoLoadBehavior.Prompt;
    }

    private static ModDatabaseAutoLoadMode ParseModDatabaseAutoLoadMode(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out ModDatabaseAutoLoadMode mode))
        {
            return mode;
        }

        return ModDatabaseAutoLoadMode.TotalDownloads;
    }

    private static double? NormalizeWindowDimension(double? dimension)
    {
        if (!dimension.HasValue)
        {
            return null;
        }

        double value = dimension.Value;
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return null;
        }

        return value;
    }

    private static double? NormalizeModInfoCoordinate(double? coordinate)
    {
        if (!coordinate.HasValue)
        {
            return null;
        }

        double value = coordinate.Value;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        return value;
    }

    private static string DetermineConfigurationPath()
    {
        string preferredDirectory = GetPreferredConfigurationDirectory();
        return Path.Combine(preferredDirectory, ConfigurationFileName);
    }

    private static string DetermineModConfigPathsPath(string fileName)
    {
        string preferredDirectory = GetPreferredConfigurationDirectory();
        return Path.Combine(preferredDirectory, fileName);
    }

    private static JsonObject BuildModConfigPathsJson(IReadOnlyDictionary<string, string> source)
    {
        var result = new JsonObject();

        foreach (var pair in source.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private JsonObject BuildInstalledColumnVisibilityJson()
    {
        var result = new JsonObject();

        foreach (var pair in _installedColumnVisibility.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static JsonObject BuildBulkUpdateModExclusionsJson(IReadOnlyDictionary<string, bool> source)
    {
        var result = new JsonObject();

        foreach (var pair in source.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || !pair.Value)
            {
                continue;
            }

            result[pair.Key] = true;
        }

        return result;
    }

    private static JsonObject BuildSkippedModVersionsJson(IReadOnlyDictionary<string, string> source)
    {
        var result = new JsonObject();

        foreach (var pair in source.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static JsonObject BuildModUsageTrackingJson(GameProfileState profile)
    {
        var counts = new JsonArray();

        foreach (var pair in profile.ModUsageSessionCounts
            .OrderBy(entry => entry.Key.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key.ModVersion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key.GameVersion, StringComparer.OrdinalIgnoreCase))
        {
            if (!pair.Key.IsValid || pair.Value <= 0)
            {
                continue;
            }

            counts.Add(new JsonObject
            {
                ["modId"] = pair.Key.ModId,
                ["modVersion"] = pair.Key.ModVersion,
                ["gameVersion"] = pair.Key.GameVersion,
                ["count"] = pair.Value
            });
        }

        return new JsonObject
        {
            ["longSessionCount"] = profile.LongRunningSessionCount,
            ["pendingPrompt"] = profile.HasPendingModUsagePrompt && counts.Count > 0,
            ["modCounts"] = counts
        };
    }

    private JsonObject BuildThemePaletteJson()
    {
        IReadOnlyDictionary<string, string> defaults = _colorTheme == ColorTheme.Custom
            ? _customThemePaletteColors
            : GetDefaultPalette(_colorTheme);
        return BuildPaletteJson(_themePaletteColors, defaults);
    }

    private JsonObject BuildCustomThemePaletteJson()
    {
        return BuildPaletteJson(_customThemePaletteColors, GetDefaultPalette(ColorTheme.Custom));
    }

    private JsonObject BuildGameProfilesJson()
    {
        var result = new JsonObject();

        foreach (GameProfileState profile in _gameProfiles.Values
                     .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var profileObject = new JsonObject
            {
                ["dataDirectory"] = profile.DataDirectory,
                ["gameDirectory"] = profile.GameDirectory,
                ["customShortcutPath"] = profile.CustomShortcutPath,
                ["bulkUpdateModExclusions"] = BuildBulkUpdateModExclusionsJson(profile.BulkUpdateModExclusions),
                ["skippedModVersions"] = BuildSkippedModVersionsJson(profile.SkippedModVersions),
                ["modConfigPaths"] = BuildModConfigPathsJson(profile.ModConfigPaths),
                ["modUsageTracking"] = BuildModUsageTrackingJson(profile)
            };

            result[profile.Name] = profileObject;
        }

        return result;
    }

    private static JsonObject BuildPaletteJson(
        IReadOnlyDictionary<string, string> palette,
        IReadOnlyDictionary<string, string> defaults)
    {
        var result = new JsonObject();

        foreach (var pair in palette.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            result[pair.Key] = pair.Value;
        }

        foreach (var pair in defaults.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (result.ContainsKey(pair.Key))
            {
                continue;
            }

            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private void LoadBulkUpdateModExclusions(JsonNode? node, Dictionary<string, bool> target)
    {
        target.Clear();

        if (node is not JsonObject obj)
        {
            return;
        }

        bool requiresSave = false;

        foreach ((string key, JsonNode? value) in obj)
        {
            string? normalized = NormalizeModId(key);
            if (normalized is null)
            {
                requiresSave = true;
                continue;
            }

            bool isExcluded = value?.GetValue<bool?>() ?? false;
            if (!isExcluded)
            {
                requiresSave = true;
                continue;
            }

            if (target.ContainsKey(normalized))
            {
                continue;
            }

            target[normalized] = true;
        }

        if (requiresSave)
        {
            _hasPendingSave = true;
        }
    }

    private void LoadSkippedModVersions(JsonNode? node, Dictionary<string, string> target)
    {
        target.Clear();

        if (node is not JsonObject obj)
        {
            return;
        }

        bool requiresSave = false;

        foreach ((string key, JsonNode? value) in obj)
        {
            string? normalizedId = NormalizeModId(key);
            if (normalizedId is null)
            {
                requiresSave = true;
                continue;
            }

            string? version = NormalizeVersion(GetOptionalString(value));
            if (version is null)
            {
                requiresSave = true;
                continue;
            }

            if (target.ContainsKey(normalizedId))
            {
                continue;
            }

            target[normalizedId] = version;
        }

        if (requiresSave)
        {
            _hasPendingSave = true;
        }
    }

    private void LoadModConfigPaths(JsonNode? node, Dictionary<string, string> target)
    {
        target.Clear();

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

            string? path = GetOptionalString(pair.Value);
            string? normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            target[modId.Trim()] = normalized;
        }
    }

    private void LoadInstalledColumnVisibility(JsonNode? node)
    {
        _installedColumnVisibility.Clear();

        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var pair in obj)
        {
            string? columnKey = NormalizeInstalledColumnKey(pair.Key);
            bool? isVisible = pair.Value?.GetValue<bool?>();

            if (columnKey is null || !isVisible.HasValue)
            {
                continue;
            }

            _installedColumnVisibility[columnKey] = isVisible.Value;
        }
    }

    private void LoadThemePalette(JsonNode? node)
    {
        IReadOnlyDictionary<string, string> defaults = GetCurrentThemeDefaults();

        if (node is not JsonObject obj)
        {
            EnsurePaletteDefaults(_themePaletteColors, defaults);
            _hasPendingSave = true;
            return;
        }

        bool requiresSave = false;
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in obj)
        {
            string key = pair.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string normalizedKey = key.Trim();
            if (!_themePaletteColors.ContainsKey(normalizedKey))
            {
                if (defaults.ContainsKey(normalizedKey))
                {
                    _themePaletteColors[normalizedKey] = defaults[normalizedKey];
                }

                continue;
            }

            string? value = GetOptionalString(pair.Value);
            if (!TryNormalizeHexColor(value, out string normalized))
            {
                requiresSave = true;
                continue;
            }

            _themePaletteColors[normalizedKey] = normalized;
            processedKeys.Add(normalizedKey);
        }

        foreach (var pair in defaults)
        {
            if (!processedKeys.Contains(pair.Key))
            {
                requiresSave = true;
            }

            if (!_themePaletteColors.ContainsKey(pair.Key))
            {
                _themePaletteColors[pair.Key] = pair.Value;
            }
        }

        if (requiresSave)
        {
            _hasPendingSave = true;
        }
    }

    private void LoadModUsageTracking(JsonNode? node, GameProfileState profile)
    {
        profile.LongRunningSessionCount = 0;
        profile.HasPendingModUsagePrompt = false;
        profile.ModUsageSessionCounts.Clear();

        if (node is not JsonObject obj)
        {
            return;
        }

        int? storedCount = obj["longSessionCount"]?.GetValue<int?>();
        profile.LongRunningSessionCount = storedCount.HasValue && storedCount.Value > 0 ? storedCount.Value : 0;
        profile.HasPendingModUsagePrompt = obj["pendingPrompt"]?.GetValue<bool?>() ?? false;

        JsonNode? countsNode = obj["modCounts"];

        if (countsNode is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                if (item is not JsonObject entry)
                {
                    continue;
                }

                string? modId = GetOptionalString(entry["modId"]);
                string? modVersion = GetOptionalString(entry["modVersion"]);
                string? gameVersion = GetOptionalString(entry["gameVersion"]);
                int? usageCount = entry["count"]?.GetValue<int?>();

                if (string.IsNullOrWhiteSpace(modId)
                    || string.IsNullOrWhiteSpace(modVersion)
                    || string.IsNullOrWhiteSpace(gameVersion)
                    || !usageCount.HasValue
                    || usageCount.Value <= 0)
                {
                    continue;
                }

                var key = new ModUsageTrackingKey(modId, modVersion, gameVersion);
                if (!key.IsValid)
                {
                    continue;
                }

                profile.ModUsageSessionCounts[key] = usageCount.Value;
            }
        }
        else if (countsNode is JsonObject legacyCounts)
        {
            foreach ((string? key, JsonNode? value) in legacyCounts)
            {
                if (string.IsNullOrWhiteSpace(key) || value is null)
                {
                    continue;
                }

                int? usageCount = value.GetValue<int?>();
                if (!usageCount.HasValue || usageCount.Value <= 0)
                {
                    continue;
                }

                string? normalizedId = NormalizeModId(key);
                if (normalizedId is null)
                {
                    continue;
                }

                _hasPendingSave = true;
            }
        }

        if (profile.ModUsageSessionCounts.Count == 0)
        {
            profile.LongRunningSessionCount = 0;
            profile.HasPendingModUsagePrompt = false;
        }
        else if (profile.LongRunningSessionCount >= GameSessionVoteThreshold)
        {
            profile.HasPendingModUsagePrompt = true;
        }
    }

    private bool LoadCustomThemePalette(JsonNode? node)
    {
        IReadOnlyDictionary<string, string> defaults = GetDefaultPalette(ColorTheme.Custom);
        _customThemePaletteColors.Clear();

        bool requiresSave = false;
        bool propertyFound;
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (node is JsonObject obj)
        {
            propertyFound = true;

            foreach (var pair in obj)
            {
                string key = pair.Key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string normalizedKey = key.Trim();
                if (!defaults.ContainsKey(normalizedKey))
                {
                    continue;
                }

                string? value = GetOptionalString(pair.Value);
                if (!TryNormalizeHexColor(value, out string normalized))
                {
                    requiresSave = true;
                    continue;
                }

                _customThemePaletteColors[normalizedKey] = normalized;
                processedKeys.Add(normalizedKey);
            }
        }
        else
        {
            propertyFound = false;
            requiresSave = true;
        }

        foreach (var pair in defaults)
        {
            if (!processedKeys.Contains(pair.Key))
            {
                requiresSave = true;
            }

            if (!_customThemePaletteColors.ContainsKey(pair.Key))
            {
                _customThemePaletteColors[pair.Key] = pair.Value;
            }
        }

        if (requiresSave)
        {
            _hasPendingSave = true;
        }

        return propertyFound;
    }

    private void SyncCustomThemePaletteWithCurrentTheme()
    {
        EnsureCustomThemePaletteInitialized();

        foreach (var pair in _themePaletteColors)
        {
            _customThemePaletteColors[pair.Key] = pair.Value;
        }

        EnsurePaletteDefaults(_customThemePaletteColors, GetDefaultPalette(ColorTheme.Custom));
    }

    private void ResetThemePaletteToDefaults()
    {
        _themePaletteColors.Clear();

        if (_colorTheme == ColorTheme.Custom)
        {
            EnsureCustomThemePaletteInitialized();
            foreach (var pair in _customThemePaletteColors)
            {
                _themePaletteColors[pair.Key] = pair.Value;
            }

            EnsurePaletteDefaults(_themePaletteColors, GetDefaultPalette(ColorTheme.Custom));
            return;
        }

        foreach (var pair in GetDefaultPalette(_colorTheme))
        {
            _themePaletteColors[pair.Key] = pair.Value;
        }
    }

    private bool ApplyThemePaletteOverride(IReadOnlyDictionary<string, string> paletteOverride)
    {
        bool changed = false;

        foreach (var pair in paletteOverride)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            string normalizedKey = pair.Key.Trim();
            if (!_themePaletteColors.ContainsKey(normalizedKey))
            {
                continue;
            }

            if (!TryNormalizeHexColor(pair.Value, out string normalizedValue))
            {
                continue;
            }

            if (!_themePaletteColors.TryGetValue(normalizedKey, out string? currentValue)
                || !string.Equals(currentValue, normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                _themePaletteColors[normalizedKey] = normalizedValue;
                if (_colorTheme == ColorTheme.Custom)
                {
                    EnsureCustomThemePaletteInitialized();
                    _customThemePaletteColors[normalizedKey] = normalizedValue;
                }
                changed = true;
            }
        }

        return changed;
    }

    private void ResetCustomThemePaletteToDefaults()
    {
        _customThemePaletteColors.Clear();

        foreach (var pair in GetDefaultPalette(ColorTheme.Custom))
        {
            _customThemePaletteColors[pair.Key] = pair.Value;
        }
    }

    private void EnsureCustomThemePaletteInitialized()
    {
        if (_customThemePaletteColors.Count == 0)
        {
            ResetCustomThemePaletteToDefaults();
            return;
        }

        EnsurePaletteDefaults(_customThemePaletteColors, GetDefaultPalette(ColorTheme.Custom));
    }

    private IReadOnlyDictionary<string, string> GetCurrentThemeDefaults()
    {
        if (_colorTheme == ColorTheme.Custom)
        {
            EnsureCustomThemePaletteInitialized();
            return _customThemePaletteColors;
        }

        return GetDefaultPalette(_colorTheme);
    }

    private static void EnsurePaletteDefaults(
        IDictionary<string, string> palette,
        IReadOnlyDictionary<string, string> defaults)
    {
        foreach (var pair in defaults)
        {
            if (!palette.ContainsKey(pair.Key))
            {
                palette[pair.Key] = pair.Value;
            }
        }
    }

    private static IReadOnlyDictionary<string, string> GetDefaultPalette(ColorTheme theme)
    {
        return theme switch
        {
            ColorTheme.VintageStory => VintageStoryPaletteColors,
            ColorTheme.Dark => DarkPaletteColors,
            ColorTheme.Light => LightPaletteColors,
            ColorTheme.SurpriseMe => VintageStoryPaletteColors,
            ColorTheme.Custom => VintageStoryPaletteColors,
            _ => VintageStoryPaletteColors
        };
    }

    private void LoadPersistentModConfigPaths()
    {
        _storedModConfigPaths.Clear();

        try
        {
            if (!File.Exists(_modConfigPathsPath))
            {
                return;
            }

            using (FileStream stream = File.OpenRead(_modConfigPathsPath))
            {
                JsonNode? node = JsonNode.Parse(stream);
                if (node is not JsonObject obj)
                {
                    return;
                }

                foreach (var pair in obj)
                {
                    string modId = pair.Key;
                    if (string.IsNullOrWhiteSpace(modId) || pair.Value is not JsonObject entryObj)
                    {
                        continue;
                    }

                    string? directoryPath = NormalizePath(GetOptionalString(entryObj["directoryPath"]));
                    string? fileName = GetOptionalString(entryObj["fileName"])?.Trim();

                    if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    var entry = new ModConfigPathEntry(directoryPath, fileName);
                    string key = modId.Trim();
                    _storedModConfigPaths[key] = entry;

                    string? combinedPath = BuildFullConfigPath(entry);
                    if (!string.IsNullOrWhiteSpace(combinedPath))
                    {
                        ActiveModConfigPaths[key] = combinedPath;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            _storedModConfigPaths.Clear();
        }
    }

    private void UpdatePersistentModConfigPath(string modId, string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        string? directoryPath = ExtractDirectoryPath(normalizedPath);
        string? fileName = ExtractFileName(normalizedPath);

        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(fileName))
        {
            if (_storedModConfigPaths.Remove(modId))
            {
                SaveModConfigPathHistory();
            }

            return;
        }

        string directory = directoryPath!;
        string file = fileName!;
        var entry = new ModConfigPathEntry(directory, file);

        if (_storedModConfigPaths.TryGetValue(modId, out ModConfigPathEntry? existing)
            && existing is not null
            && string.Equals(existing.DirectoryPath, entry.DirectoryPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.FileName, entry.FileName, StringComparison.Ordinal))
        {
            return;
        }

        _storedModConfigPaths[modId] = entry;
        SaveModConfigPathHistory();
    }

    private void SaveModConfigPathHistory()
    {
        _hasPendingModConfigPathSave = true;

        if (!_isPersistenceEnabled)
        {
            return;
        }

        PersistModConfigPathHistory();
    }

    private void PersistModConfigPathHistory()
    {
        try
        {
            string directory = Path.GetDirectoryName(_modConfigPathsPath)!;
            Directory.CreateDirectory(directory);

            var root = new JsonObject();

            foreach (var pair in _storedModConfigPaths.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var entry = pair.Value;
                root[pair.Key] = new JsonObject
                {
                    ["directoryPath"] = entry.DirectoryPath,
                    ["fileName"] = entry.FileName
                };
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(_modConfigPathsPath, root.ToJsonString(options));
            _hasPendingModConfigPathSave = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Persisting the configuration is a best-effort attempt. Ignore failures silently.
        }
    }

    private static string? BuildFullConfigPath(ModConfigPathEntry entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.DirectoryPath) || string.IsNullOrWhiteSpace(entry.FileName))
        {
            return null;
        }

        string combined = Path.Combine(entry.DirectoryPath, entry.FileName);
        return NormalizePath(combined) ?? combined;
    }

    private static string? ExtractDirectoryPath(string normalizedPath)
    {
        string? directory = Path.GetDirectoryName(normalizedPath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.GetPathRoot(normalizedPath);
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return NormalizePath(directory);
    }

    private static string? ExtractFileName(string normalizedPath)
    {
        string trimmed = normalizedPath.Trim();
        string fileName = Path.GetFileName(trimmed);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return fileName;
    }

    private sealed class ModConfigPathEntry
    {
        public ModConfigPathEntry(string directoryPath, string fileName)
        {
            DirectoryPath = directoryPath;
            FileName = fileName;
        }

        public string DirectoryPath { get; }

        public string FileName { get; }
    }

    private static string GetPreferredConfigurationDirectory()
    {
        string? localAppData = GetFolder(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "Simple VS Manager");
        }

        string? appData = GetFolder(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            return Path.Combine(appData, "Simple VS Manager");
        }

        string? documents = GetFolder(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return Path.Combine(documents, "Simple VS Manager");
        }

        string? personal = GetFolder(Environment.SpecialFolder.Personal);
        if (!string.IsNullOrWhiteSpace(personal))
        {
            return Path.Combine(personal, "Simple VS Manager");
        }

        string? userProfile = GetFolder(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, ".simple-vs-manager");
        }

        return Path.Combine(AppContext.BaseDirectory, "Simple VS Manager");
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

    private static bool TryNormalizeHexColor(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (!trimmed.StartsWith('#') || trimmed.Length <= 1)
        {
            return false;
        }

        string hex = trimmed[1..];
        if (hex.Length is not 6 and not 8)
        {
            return false;
        }

        foreach (char c in hex)
        {
            if (!IsHexDigit(c))
            {
                return false;
            }
        }

        normalized = "#" + hex.ToUpperInvariant();
        return true;
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'A' && c <= 'F')
            || (c >= 'a' && c <= 'f');
    }

    private static string? GetOptionalString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<string?>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ResolveCurrentVersion()
    {
        Assembly assembly = typeof(UserConfigurationService).Assembly;

        string? version = TrimToSemanticVersion(
            VersionStringUtility.Normalize(
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion));
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version!;
        }

        version = TrimToSemanticVersion(
            VersionStringUtility.Normalize(
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version));
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version!;
        }

        version = TrimToSemanticVersion(VersionStringUtility.Normalize(assembly.GetName().Version?.ToString()));
        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version!;
    }

    private static string? TrimToSemanticVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        string[] parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (parts.Length <= 3)
        {
            return string.Join('.', parts);
        }

        return string.Join('.', parts.Take(3));
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        string? normalized = VersionStringUtility.Normalize(version);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

        string trimmed = sortMemberPath.Trim();

        if (string.Equals(trimmed, nameof(ModListItemViewModel.DisplayName), StringComparison.OrdinalIgnoreCase))
        {
            return nameof(ModListItemViewModel.NameSortKey);
        }

        return trimmed;
    }

    private static string? NormalizeInstalledColumnKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        return columnName.Trim();
    }

    private static string? NormalizeModId(string? modId)
    {
        return string.IsNullOrWhiteSpace(modId) ? null : modId.Trim();
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

    private static string? NormalizeUploaderName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
