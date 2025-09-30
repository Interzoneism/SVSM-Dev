using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// Main view model that coordinates mod discovery and activation.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ObservableCollection<ModListItemViewModel> _mods = new();
    private readonly ClientSettingsStore _settingsStore;
    private readonly ModDiscoveryService _discoveryService;
    private readonly ModDatabaseService _databaseService;
    private readonly ObservableCollection<SortOption> _sortOptions;
    private readonly ObservableCollection<string> _presets = new();
    private readonly string? _installedGameVersion;
    private readonly UserConfigurationService _userConfiguration;
    private readonly ReadOnlyObservableCollection<string> _presetsView;

    private SortOption? _selectedSortOption;
    private string? _selectedPreset;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _isErrorStatus;
    private int _totalMods;
    private int _activeMods;
    private bool _suppressPresetApplication;

    public MainViewModel(string dataDirectory, UserConfigurationService userConfiguration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(userConfiguration);

        DataDirectory = Path.GetFullPath(dataDirectory);

        _userConfiguration = userConfiguration;
        _settingsStore = new ClientSettingsStore(DataDirectory);
        _discoveryService = new ModDiscoveryService(_settingsStore);
        _databaseService = new ModDatabaseService();
        _installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion();

        ModsView = CollectionViewSource.GetDefaultView(_mods);
        _sortOptions = new ObservableCollection<SortOption>(CreateSortOptions());
        SortOptions = new ReadOnlyObservableCollection<SortOption>(_sortOptions);
        _presetsView = new ReadOnlyObservableCollection<string>(_presets);
        LoadPresetList();
        SetSelectedPresetWithoutApplying(_userConfiguration.SelectedPreset);
        SelectedSortOption = SortOptions.FirstOrDefault();
        SelectedSortOption?.Apply(ModsView);

        RefreshCommand = new AsyncRelayCommand(LoadModsAsync);
        SetStatus("Ready.", false);
    }

    public string DataDirectory { get; }

    public ICollectionView ModsView { get; }

    public ReadOnlyObservableCollection<SortOption> SortOptions { get; }

    public ReadOnlyObservableCollection<string> Presets => _presetsView;

    public SortOption? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                value?.Apply(ModsView);
            }
        }
    }

    public string? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            string? previous = _selectedPreset;
            if (EqualityComparer<string?>.Default.Equals(previous, value))
            {
                return;
            }

            if (SetProperty(ref _selectedPreset, value))
            {
                if (_suppressPresetApplication)
                {
                    return;
                }

                if (!ApplyPresetInternal(value, showStatus: true, persistSelection: true))
                {
                    SetSelectedPresetWithoutApplying(previous);
                }
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsErrorStatus
    {
        get => _isErrorStatus;
        private set => SetProperty(ref _isErrorStatus, value);
    }

    public int TotalMods
    {
        get => _totalMods;
        private set
        {
            if (SetProperty(ref _totalMods, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public int ActiveMods
    {
        get => _activeMods;
        private set
        {
            if (SetProperty(ref _activeMods, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public string SummaryText => TotalMods == 0
        ? "No mods found."
        : $"{ActiveMods} active of {TotalMods} mods";

    public IAsyncRelayCommand RefreshCommand { get; }

    public Task InitializeAsync() => LoadModsAsync();

    public bool ContainsPreset(string name) => _userConfiguration.ContainsPreset(name);

    public bool TrySavePreset(string name, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Preset name cannot be empty.";
            return false;
        }

        IReadOnlyList<string> disabledEntries = CaptureDisabledEntries();

        string actualName;
        try
        {
            actualName = _userConfiguration.SetPreset(name, disabledEntries);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        AddOrUpdatePresetName(actualName);
        SetSelectedPresetWithoutApplying(actualName);
        _userConfiguration.SetSelectedPreset(actualName);
        SetStatus($"Saved preset \"{actualName}\".", false);
        errorMessage = null;
        return true;
    }

    internal async Task<ActivationResult> ApplyActivationChangeAsync(ModListItemViewModel mod, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(mod);

        string? localError = null;
        bool success = await Task.Run(() =>
        {
            bool result = _settingsStore.TrySetActive(mod.ModId, mod.Version, isActive, out var error);
            localError = error;
            return result;
        });

        if (!success)
        {
            string message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to update {mod.DisplayName}."
                : localError!;
            SetStatus(message, true);
            return new ActivationResult(false, message);
        }

        UpdateActiveCount();
        SetStatus(isActive ? $"Activated {mod.DisplayName}." : $"Deactivated {mod.DisplayName}.", false);
        return new ActivationResult(true, null);
    }

    private async Task LoadModsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetStatus("Loading mods...", false);

        try
        {
            var entries = await Task.Run(_discoveryService.LoadMods);
            await _databaseService.PopulateModDatabaseInfoAsync(entries, _installedGameVersion);
            var viewModels = entries
                .Select(entry => CreateModViewModel(entry))
                .ToList();

            _mods.Clear();
            foreach (var item in viewModels)
            {
                _mods.Add(item);
            }

            TotalMods = _mods.Count;
            UpdateActiveCount();
            SelectedSortOption?.Apply(ModsView);
            if (!string.IsNullOrWhiteSpace(_selectedPreset))
            {
                ApplyPresetInternal(_selectedPreset, showStatus: false, persistSelection: false);
            }
            SetStatus($"Loaded {TotalMods} mods.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load mods: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ModListItemViewModel CreateModViewModel(ModEntry entry)
    {
        bool isActive = !_settingsStore.IsDisabled(entry.ModId, entry.Version);
        string location = GetDisplayPath(entry.SourcePath);
        return new ModListItemViewModel(entry, isActive, location, ApplyActivationChangeAsync, _installedGameVersion);
    }

    private void UpdateActiveCount()
    {
        ActiveMods = _mods.Count(item => item.IsActive);
    }

    private string GetDisplayPath(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        string best;
        try
        {
            best = Path.GetFullPath(fullPath);
        }
        catch (Exception)
        {
            return fullPath;
        }

        foreach (var candidate in EnumerateBasePaths())
        {
            try
            {
                string relative = Path.GetRelativePath(candidate, best);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && relative.Length < best.Length)
                {
                    best = relative;
                }
            }
            catch (Exception)
            {
                // Ignore invalid paths.
            }
        }

        return best.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private IEnumerable<string> EnumerateBasePaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            try
            {
                string full = Path.GetFullPath(candidate);
                set.Add(full);
            }
            catch (Exception)
            {
                // Ignore invalid paths.
            }
        }

        TryAdd(_settingsStore.DataDirectory);
        foreach (var path in _settingsStore.SearchBaseCandidates)
        {
            TryAdd(path);
        }

        TryAdd(Directory.GetCurrentDirectory());

        return set;
    }

    private static IEnumerable<SortOption> CreateSortOptions()
    {
        yield return new SortOption("Name (A → Z)", (nameof(ModListItemViewModel.DisplayName), ListSortDirection.Ascending));
        yield return new SortOption("Name (Z → A)", (nameof(ModListItemViewModel.DisplayName), ListSortDirection.Descending));
        yield return new SortOption(
            "Active (Active → Inactive)",
            (nameof(ModListItemViewModel.IsActive), ListSortDirection.Descending),
            (nameof(ModListItemViewModel.DisplayName), ListSortDirection.Ascending));
        yield return new SortOption(
            "Active (Inactive → Active)",
            (nameof(ModListItemViewModel.IsActive), ListSortDirection.Ascending),
            (nameof(ModListItemViewModel.DisplayName), ListSortDirection.Ascending));
    }

    private void LoadPresetList()
    {
        _presets.Clear();
        foreach (string name in _userConfiguration.GetPresetNames())
        {
            _presets.Add(name);
        }
    }

    private void AddOrUpdatePresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string trimmed = name.Trim();
        for (int i = 0; i < _presets.Count; i++)
        {
            if (string.Equals(_presets[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(_presets[i], trimmed, StringComparison.Ordinal))
                {
                    _presets[i] = trimmed;
                }

                return;
            }
        }

        int insertIndex = 0;
        while (insertIndex < _presets.Count && string.Compare(_presets[insertIndex], trimmed, StringComparison.OrdinalIgnoreCase) < 0)
        {
            insertIndex++;
        }

        _presets.Insert(insertIndex, trimmed);
    }

    private bool RemovePresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < _presets.Count; i++)
        {
            if (string.Equals(_presets[i], name, StringComparison.OrdinalIgnoreCase))
            {
                _presets.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private void SetSelectedPresetWithoutApplying(string? value)
    {
        if (EqualityComparer<string?>.Default.Equals(_selectedPreset, value))
        {
            return;
        }

        _suppressPresetApplication = true;
        try
        {
            SetProperty(ref _selectedPreset, value, nameof(SelectedPreset));
        }
        finally
        {
            _suppressPresetApplication = false;
        }
    }

    private IReadOnlyList<string> CaptureDisabledEntries()
    {
        var entries = new List<string>();
        var lookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in _mods)
        {
            if (!mod.IsActive)
            {
                string key = CreateDisabledKey(mod);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (lookup.Add(key))
                {
                    entries.Add(key);
                }
            }
        }

        foreach (string entry in _settingsStore.DisabledEntries)
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

        return entries;
    }

    private static string CreateDisabledKey(ModListItemViewModel mod)
    {
        string modId = mod.ModId?.Trim() ?? string.Empty;
        if (modId.Length == 0)
        {
            return string.Empty;
        }

        string? version = mod.Version;
        if (string.IsNullOrWhiteSpace(version))
        {
            return modId;
        }

        return $"{modId}@{version.Trim()}";
    }

    private bool ApplyPresetInternal(string? presetName, bool showStatus, bool persistSelection)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            if (persistSelection)
            {
                _userConfiguration.SetSelectedPreset(null);
            }

            return true;
        }

        if (!_userConfiguration.TryGetPreset(presetName, out string actualName, out IReadOnlyList<string> disabledEntries))
        {
            if (RemovePresetName(presetName))
            {
                if (persistSelection)
                {
                    _userConfiguration.SetSelectedPreset(null);
                }

                SetSelectedPresetWithoutApplying(null);
            }

            if (showStatus)
            {
                SetStatus($"Preset \"{presetName}\" was not found.", true);
            }

            return false;
        }

        if (!string.Equals(actualName, _selectedPreset, StringComparison.Ordinal))
        {
            SetSelectedPresetWithoutApplying(actualName);
        }

        if (!_settingsStore.TryApplyPreset(disabledEntries, out string? error))
        {
            if (showStatus)
            {
                string message = string.IsNullOrWhiteSpace(error)
                    ? $"Failed to apply preset \"{actualName}\"."
                    : error!;
                SetStatus(message, true);
            }

            return false;
        }

        ApplyPresetToView(disabledEntries);

        if (persistSelection)
        {
            _userConfiguration.SetSelectedPreset(actualName);
        }

        if (showStatus)
        {
            SetStatus($"Applied preset \"{actualName}\".", false);
        }

        return true;
    }

    private void ApplyPresetToView(IReadOnlyList<string> disabledEntries)
    {
        var disabledSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in disabledEntries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            disabledSet.Add(entry.Trim());
        }

        foreach (var mod in _mods)
        {
            bool shouldBeActive = !IsModDisabled(mod, disabledSet);
            mod.SetActiveFromPreset(shouldBeActive);
        }

        UpdateActiveCount();
    }

    private static bool IsModDisabled(ModListItemViewModel mod, HashSet<string> disabledSet)
    {
        if (disabledSet.Count == 0)
        {
            return false;
        }

        string modId = mod.ModId?.Trim() ?? string.Empty;
        if (modId.Length == 0)
        {
            return false;
        }

        if (disabledSet.Contains(modId))
        {
            return true;
        }

        string? version = mod.Version;
        if (!string.IsNullOrWhiteSpace(version))
        {
            string key = $"{modId}@{version.Trim()}";
            if (disabledSet.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsErrorStatus = isError;
    }
}
