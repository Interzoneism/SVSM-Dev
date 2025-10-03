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
    private readonly string? _installedGameVersion;
    private readonly object _modsStateLock = new();

    private SortOption? _selectedSortOption;
    private bool _isBusy;
    private bool _isCompactView;
    private string _statusMessage = string.Empty;
    private bool _isErrorStatus;
    private int _totalMods;
    private int _activeMods;
    private string? _modsStateFingerprint;
    private ModListItemViewModel? _selectedMod;

    public MainViewModel(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);

        _settingsStore = new ClientSettingsStore(DataDirectory);
        _discoveryService = new ModDiscoveryService(_settingsStore);
        _databaseService = new ModDatabaseService();
        _installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion();

        ModsView = CollectionViewSource.GetDefaultView(_mods);
        _sortOptions = new ObservableCollection<SortOption>(CreateSortOptions());
        SortOptions = new ReadOnlyObservableCollection<SortOption>(_sortOptions);
        SelectedSortOption = SortOptions.FirstOrDefault();
        SelectedSortOption?.Apply(ModsView);

        RefreshCommand = new AsyncRelayCommand(LoadModsAsync);
        SetStatus("Ready.", false);
    }

    public string DataDirectory { get; }

    public ICollectionView ModsView { get; }

    public ReadOnlyObservableCollection<SortOption> SortOptions { get; }

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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsCompactView
    {
        get => _isCompactView;
        set => SetProperty(ref _isCompactView, value);
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

    public ModListItemViewModel? SelectedMod
    {
        get => _selectedMod;
        private set
        {
            if (SetProperty(ref _selectedMod, value))
            {
                OnPropertyChanged(nameof(HasSelectedMod));
            }
        }
    }

    public bool HasSelectedMod => SelectedMod != null;

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

    public IReadOnlyList<string> GetCurrentDisabledEntries()
    {
        return _settingsStore.GetDisabledEntriesSnapshot();
    }

    public async Task<bool> ApplyPresetAsync(string presetName, IReadOnlyList<string> disabledEntries)
    {
        string? localError = null;
        IReadOnlyList<string> entries = disabledEntries ?? Array.Empty<string>();

        bool success = await Task.Run(() =>
        {
            bool result = _settingsStore.TryApplyDisabledEntries(entries, out var error);
            localError = error;
            return result;
        });

        if (!success)
        {
            string message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to apply preset \"{presetName}\"."
                : localError!;
            SetStatus(message, true);
            return false;
        }

        foreach (var mod in _mods)
        {
            bool isDisabled = _settingsStore.IsDisabled(mod.ModId, mod.Version);
            mod.SetIsActiveSilently(!isDisabled);
        }

        UpdateActiveCount();
        SelectedSortOption?.Apply(ModsView);
        ModsView.Refresh();
        SetStatus($"Applied preset \"{presetName}\".", false);
        return true;
    }

    public void ReportStatus(string message, bool isError = false)
    {
        SetStatus(message, isError);
    }

    internal void SetSelectedMod(ModListItemViewModel? mod)
    {
        SelectedMod = mod;
    }

    internal async Task<bool> PreserveActivationStateAsync(string modId, string? previousVersion, string? newVersion, bool wasActive)
    {
        string? localError = null;

        bool success = await Task.Run(() =>
            _settingsStore.TryUpdateDisabledEntry(modId, previousVersion, newVersion, shouldDisable: !wasActive, out localError));

        if (!success)
        {
            string message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to preserve the activation state for {modId}."
                : localError!;
            SetStatus(message, true);
        }

        return success;
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
            SetStatus($"Loaded {TotalMods} mods.", false);
            await UpdateModsStateSnapshotAsync();
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

    public async Task<bool> CheckForModStateChangesAsync()
    {
        string? fingerprint = await CaptureModsStateFingerprintAsync().ConfigureAwait(false);
        if (fingerprint is null)
        {
            return false;
        }

        lock (_modsStateLock)
        {
            if (_modsStateFingerprint is null)
            {
                _modsStateFingerprint = fingerprint;
                return false;
            }

            if (!string.Equals(_modsStateFingerprint, fingerprint, StringComparison.Ordinal))
            {
                _modsStateFingerprint = fingerprint;
                return true;
            }
        }

        return false;
    }

    private ModListItemViewModel CreateModViewModel(ModEntry entry)
    {
        bool isActive = !_settingsStore.IsDisabled(entry.ModId, entry.Version);
        string location = GetDisplayPath(entry.SourcePath);
        return new ModListItemViewModel(entry, isActive, location, ApplyActivationChangeAsync, _installedGameVersion);
    }

    private async Task UpdateModsStateSnapshotAsync()
    {
        string? fingerprint = await CaptureModsStateFingerprintAsync().ConfigureAwait(false);
        if (fingerprint is null)
        {
            return;
        }

        lock (_modsStateLock)
        {
            _modsStateFingerprint = fingerprint;
        }
    }

    private Task<string?> CaptureModsStateFingerprintAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                return _discoveryService.GetModsStateFingerprint();
            }
            catch (Exception)
            {
                return null;
            }
        });
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

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsErrorStatus = isError;
    }
}
