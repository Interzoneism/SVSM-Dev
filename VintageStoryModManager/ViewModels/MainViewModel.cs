using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
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
    private const int RecentlyUpdatedSearchResultLimit = 30;
    private static readonly TimeSpan ModDatabaseSearchDebounce = TimeSpan.FromMilliseconds(320);

    private readonly ObservableCollection<ModListItemViewModel> _mods = new();
    private readonly Dictionary<string, ModEntry> _modEntriesBySourcePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModListItemViewModel> _modViewModelsBySourcePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<ModListItemViewModel> _searchResults = new();
    private readonly ClientSettingsStore _settingsStore;
    private readonly ModDiscoveryService _discoveryService;
    private readonly ModDatabaseService _databaseService;
    private readonly int _modDatabaseSearchResultLimit;
    private readonly ObservableCollection<SortOption> _sortOptions;
    private readonly string? _installedGameVersion;
    private readonly object _modsStateLock = new();
    private readonly ModDirectoryWatcher _modsWatcher;

    private SortOption? _selectedSortOption;
    private bool _isBusy;
    private bool _isCompactView;
    private bool _useModDbDesignView;
    private bool _isModInfoExpanded = true;
    private string _statusMessage = string.Empty;
    private bool _isErrorStatus;
    private int _totalMods;
    private int _activeMods;
    private string? _modsStateFingerprint;
    private ModListItemViewModel? _selectedMod;
    private bool _hasSelectedMods;
    private bool _hasMultipleSelectedMods;
    private string _searchText = string.Empty;
    private string[] _searchTokens = Array.Empty<string>();
    private bool _searchModDatabase;
    private CancellationTokenSource? _modDatabaseSearchCts;
    private readonly RelayCommand _clearSearchCommand;
    private readonly RelayCommand _showInstalledModsCommand;
    private readonly RelayCommand _showModDatabaseCommand;
    private bool _showRecentlyUpdatedOnly;
    private bool _showPopularRecently;

    public MainViewModel(string dataDirectory, int modDatabaseSearchResultLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);

        _settingsStore = new ClientSettingsStore(DataDirectory);
        _discoveryService = new ModDiscoveryService(_settingsStore);
        _databaseService = new ModDatabaseService();
        _modDatabaseSearchResultLimit = Math.Clamp(modDatabaseSearchResultLimit, 1, 100);
        _installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion();
        _modsWatcher = new ModDirectoryWatcher(_discoveryService);

        ModsView = CollectionViewSource.GetDefaultView(_mods);
        ModsView.Filter = FilterMod;
        SearchResultsView = CollectionViewSource.GetDefaultView(_searchResults);
        _sortOptions = new ObservableCollection<SortOption>(CreateSortOptions());
        SortOptions = new ReadOnlyObservableCollection<SortOption>(_sortOptions);
        SelectedSortOption = SortOptions.FirstOrDefault();
        SelectedSortOption?.Apply(ModsView);

        _clearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => HasSearchText);
        ClearSearchCommand = _clearSearchCommand;

        _showInstalledModsCommand = new RelayCommand(() => SearchModDatabase = false);
        _showModDatabaseCommand = new RelayCommand(() => SearchModDatabase = true);
        ShowInstalledModsCommand = _showInstalledModsCommand;
        ShowModDatabaseCommand = _showModDatabaseCommand;

        RefreshCommand = new AsyncRelayCommand(LoadModsAsync);
        SetStatus("Ready.", false);
    }

    public string DataDirectory { get; }

    public ICollectionView ModsView { get; }

    public ICollectionView SearchResultsView { get; }

    public ICollectionView CurrentModsView => SearchModDatabase ? SearchResultsView : ModsView;

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

    public bool UseModDbDesignView
    {
        get => _useModDbDesignView;
        set => SetProperty(ref _useModDbDesignView, value);
    }

    public bool IsModInfoExpanded
    {
        get => _isModInfoExpanded;
        set => SetProperty(ref _isModInfoExpanded, value);
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

    public bool HasSelectedMods
    {
        get => _hasSelectedMods;
        private set => SetProperty(ref _hasSelectedMods, value);
    }

    public bool HasMultipleSelectedMods
    {
        get => _hasMultipleSelectedMods;
        private set => SetProperty(ref _hasMultipleSelectedMods, value);
    }

    public bool IsErrorStatus
    {
        get => _isErrorStatus;
        private set => SetProperty(ref _isErrorStatus, value);
    }

    public bool SearchModDatabase
    {
        get => _searchModDatabase;
        set
        {
            if (SetProperty(ref _searchModDatabase, value))
            {
                _modDatabaseSearchCts?.Cancel();

                if (value)
                {
                    ClearSearchResults();
                    SelectedMod = null;

                    TriggerModDatabaseSearch();
                }
                else
                {
                    ClearSearchResults();
                    SelectedSortOption?.Apply(ModsView);
                    ModsView.Refresh();
                    SetStatus("Showing installed mods.", false);
                }

                OnPropertyChanged(nameof(CurrentModsView));
            }
        }
    }

    public IRelayCommand ShowInstalledModsCommand { get; }

    public IRelayCommand ShowModDatabaseCommand { get; }

    public bool ShowRecentlyUpdatedOnly
    {
        get => _showRecentlyUpdatedOnly;
        set
        {
            if (SetProperty(ref _showRecentlyUpdatedOnly, value) && SearchModDatabase)
            {
                TriggerModDatabaseSearch();
            }
        }
    }

    public bool ShowPopularRecently
    {
        get => _showPopularRecently;
        set
        {
            if (SetProperty(ref _showPopularRecently, value) && SearchModDatabase)
            {
                TriggerModDatabaseSearch();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            string newValue = value ?? string.Empty;
            if (SetProperty(ref _searchText, newValue))
            {
                _searchTokens = CreateSearchTokens(newValue);
                OnPropertyChanged(nameof(HasSearchText));
                _clearSearchCommand.NotifyCanExecuteChanged();
                if (SearchModDatabase)
                {
                    TriggerModDatabaseSearch();
                }
                else
                {
                    ModsView.Refresh();
                }
            }
        }
    }

    public bool HasSearchText => _searchTokens.Length > 0;

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

    public IRelayCommand ClearSearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public Task InitializeAsync() => LoadModsAsync();

    public IReadOnlyList<string> GetCurrentDisabledEntries()
    {
        return _settingsStore.GetDisabledEntriesSnapshot();
    }

    public IReadOnlyList<ModPresetModState> GetCurrentModStates()
    {
        return _mods
            .Select(mod => new ModPresetModState(mod.ModId, mod.Version, mod.IsActive))
            .ToList();
    }

    public async Task<bool> ApplyPresetAsync(ModPreset preset)
    {
        string? localError = null;

        bool success;
        if (preset.IncludesModStatus && preset.ModStates.Count > 0)
        {
            success = await Task.Run(() =>
            {
                foreach (var state in preset.ModStates)
                {
                    if (state is null || string.IsNullOrWhiteSpace(state.ModId) || state.IsActive is not bool desiredState)
                    {
                        continue;
                    }

                    string normalizedId = state.ModId.Trim();
                    string? version = preset.IncludesModVersions
                        ? (string.IsNullOrWhiteSpace(state.Version) ? null : state.Version!.Trim())
                        : null;

                    if (!_settingsStore.TrySetActive(normalizedId, version, desiredState, out string? error))
                    {
                        localError = error;
                        return false;
                    }
                }

                localError = null;
                return true;
            });
        }
        else
        {
            IReadOnlyList<string> entries = preset.DisabledEntries ?? Array.Empty<string>();

            success = await Task.Run(() =>
            {
                bool result = _settingsStore.TryApplyDisabledEntries(entries, out var error);
                localError = error;
                return result;
            });
        }

        if (!success)
        {
            string message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to apply preset \"{preset.Name}\"."
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
        SetStatus($"Applied preset \"{preset.Name}\".", false);
        return true;
    }

    public void ReportStatus(string message, bool isError = false)
    {
        SetStatus(message, isError);
    }

    internal void SetSelectedMod(ModListItemViewModel? mod, int selectionCount)
    {
        HasSelectedMods = selectionCount > 0;
        HasMultipleSelectedMods = selectionCount > 1;
        SelectedMod = mod;
    }

    internal void RemoveSearchResult(ModListItemViewModel mod)
    {
        if (mod is null)
        {
            return;
        }

        _searchResults.Remove(mod);

        if (ReferenceEquals(SelectedMod, mod))
        {
            SelectedMod = null;
        }
    }

    internal ModListItemViewModel? FindInstalledModById(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        foreach (var mod in _mods)
        {
            if (string.Equals(mod.ModId, modId, StringComparison.OrdinalIgnoreCase))
            {
                return mod;
            }
        }

        return null;
    }

    internal string? InstalledGameVersion => _installedGameVersion;

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

    internal async Task RefreshModsWithErrorsAsync()
    {
        if (_mods.Count == 0 || _modEntriesBySourcePath.Count == 0)
        {
            return;
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in _mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.SourcePath))
            {
                continue;
            }

            if (mod.HasLoadError || mod.DependencyHasErrors)
            {
                candidates.Add(mod.SourcePath);
            }
        }

        var entries = new List<ModEntry>(_modEntriesBySourcePath.Values);

        await Task.Run(() => _discoveryService.ApplyLoadStatuses(entries)).ConfigureAwait(true);

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.SourcePath))
            {
                continue;
            }

            if (entry.HasLoadError
                || entry.DependencyHasErrors
                || (entry.MissingDependencies?.Count ?? 0) > 0)
            {
                candidates.Add(entry.SourcePath);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (string path in candidates)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (_modEntriesBySourcePath.TryGetValue(path, out var entry)
                && _modViewModelsBySourcePath.TryGetValue(path, out var viewModel))
            {
                viewModel.UpdateLoadError(entry.LoadError);
                viewModel.UpdateDependencyIssues(entry.DependencyHasErrors, entry.MissingDependencies);
            }
        }
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
            _modsWatcher.EnsureWatchers();
            ModDirectoryChangeSet changeSet = _modsWatcher.ConsumeChanges();
            bool requiresFullReload = _mods.Count == 0
                || changeSet.RequiresFullRescan
                || changeSet.Paths.Count == 0;

            string? previousSelection = SelectedMod?.SourcePath;

            if (requiresFullReload)
            {
                var entries = await Task.Run(_discoveryService.LoadMods);
                var entryList = entries.ToList();
                _discoveryService.ApplyLoadStatuses(entryList);
                ApplyFullReload(entryList, previousSelection);
                QueueDatabaseInfoRefresh(entryList);
                SetStatus($"Loaded {TotalMods} mods.", false);
            }
            else
            {
                var reloadResults = await Task.Run(() => LoadChangedModEntries(changeSet.Paths));

                foreach (var pair in reloadResults)
                {
                    if (pair.Value == null)
                    {
                        _modEntriesBySourcePath.Remove(pair.Key);
                    }
                    else
                    {
                        _modEntriesBySourcePath[pair.Key] = pair.Value;
                    }
                }

                var updatedEntries = new List<ModEntry>();
                foreach (var entry in reloadResults.Values)
                {
                    if (entry != null)
                    {
                        updatedEntries.Add(entry);
                    }
                }

                var allEntries = new List<ModEntry>(_modEntriesBySourcePath.Values);
                _discoveryService.ApplyLoadStatuses(allEntries);

                ApplyPartialUpdates(reloadResults, previousSelection);

                if (updatedEntries.Count > 0)
                {
                    QueueDatabaseInfoRefresh(updatedEntries);
                }
            }

            TotalMods = _mods.Count;
            UpdateActiveCount();
            SelectedSortOption?.Apply(ModsView);
            ModsView.Refresh();
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
        _modsWatcher.EnsureWatchers();

        if (_modsWatcher.HasPendingChanges)
        {
            return true;
        }

        if (_modsWatcher.IsWatching)
        {
            return false;
        }

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
        return new ModListItemViewModel(entry, isActive, location, ApplyActivationChangeAsync, _installedGameVersion, true);
    }

    private async Task UpdateModsStateSnapshotAsync()
    {
        if (_modsWatcher.IsWatching)
        {
            lock (_modsStateLock)
            {
                _modsStateFingerprint = null;
            }

            return;
        }

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

    private void ClearSearchResults()
    {
        if (_searchResults.Count == 0)
        {
            SelectedMod = null;
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() =>
            {
                _searchResults.Clear();
                SelectedMod = null;
            });
            return;
        }

        _searchResults.Clear();
        SelectedMod = null;
    }

    private void TriggerModDatabaseSearch()
    {
        if (!SearchModDatabase)
        {
            return;
        }

        _modDatabaseSearchCts?.Cancel();

        var cts = new CancellationTokenSource();
        _modDatabaseSearchCts = cts;

        bool hasSearchTokens = HasSearchText;
        string statusMessage;
        if (hasSearchTokens)
        {
            statusMessage = ShowRecentlyUpdatedOnly
                ? "Searching for recently updated mods..."
                : "Searching the mod database...";
        }
        else if (ShowRecentlyUpdatedOnly)
        {
            statusMessage = "Loading recently updated mods...";
        }
        else if (ShowPopularRecently)
        {
            statusMessage = "Loading recently popular mods...";
        }
        else
        {
            statusMessage = "Loading popular mods...";
        }
        SetStatus(statusMessage, false);

        _ = RunModDatabaseSearchAsync(SearchText, hasSearchTokens, cts);
    }

    private async Task RunModDatabaseSearchAsync(string query, bool hasSearchTokens, CancellationTokenSource cts)
    {
        CancellationToken cancellationToken = cts.Token;

        try
        {
            await Task.Delay(ModDatabaseSearchDebounce, cancellationToken).ConfigureAwait(false);

            int maxResults = ShowRecentlyUpdatedOnly
                ? RecentlyUpdatedSearchResultLimit
                : _modDatabaseSearchResultLimit;

            IReadOnlyList<ModDatabaseSearchResult> results;
            if (ShowRecentlyUpdatedOnly)
            {
                results = hasSearchTokens
                    ? await _databaseService.SearchRecentlyUpdatedModsAsync(query, maxResults, cancellationToken)
                        .ConfigureAwait(false)
                    : await _databaseService.GetRecentlyUpdatedModsAsync(maxResults, cancellationToken)
                        .ConfigureAwait(false);
            }
            else if (!hasSearchTokens && ShowPopularRecently)
            {
                results = await _databaseService.GetPopularRecentlyModsAsync(maxResults, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                results = hasSearchTokens
                    ? await _databaseService.SearchModsAsync(query, maxResults, cancellationToken)
                        .ConfigureAwait(false)
                    : await _databaseService.GetMostDownloadedModsAsync(maxResults, cancellationToken)
                        .ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            IReadOnlyList<ModDatabaseSearchResult> filteredResults = ApplyModDatabaseFilters(results, maxResults);

            if (filteredResults.Count == 0)
            {
                await UpdateSearchResultsAsync(Array.Empty<ModListItemViewModel>(), cancellationToken).ConfigureAwait(false);
                await InvokeOnDispatcherAsync(
                        () => SetStatus(BuildNoModDatabaseResultsMessage(hasSearchTokens), false),
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            HashSet<string> installedModIds = await GetInstalledModIdsAsync(cancellationToken).ConfigureAwait(false);

            var entries = filteredResults
                .Select(result => new
                {
                    Entry = CreateSearchResultEntry(result),
                    IsInstalled = IsResultInstalled(result, installedModIds)
                })
                .ToList();

            if (entries.Count == 0)
            {
                await UpdateSearchResultsAsync(Array.Empty<ModListItemViewModel>(), cancellationToken).ConfigureAwait(false);
                await InvokeOnDispatcherAsync(
                        () => SetStatus(BuildNoModDatabaseResultsMessage(hasSearchTokens), false),
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var viewModels = entries
                .Select(item => CreateSearchResultViewModel(item.Entry, item.IsInstalled))
                .ToList();

            await UpdateSearchResultsAsync(viewModels, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeOnDispatcherAsync(
                    () => SetStatus("Loading mod details...", false),
                    cancellationToken)
                .ConfigureAwait(false);

            await _databaseService.PopulateModDatabaseInfoAsync(entries.Select(item => item.Entry), _installedGameVersion, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeOnDispatcherAsync(
                () =>
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        ModDatabaseInfo? info = entries[i].Entry.DatabaseInfo;
                        if (info != null)
                        {
                            viewModels[i].UpdateDatabaseInfo(info, loadLogoImmediately: false);
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeOnDispatcherAsync(
                    () => SetStatus("Loading mod images...", false),
                    cancellationToken)
                .ConfigureAwait(false);

            await LoadModDatabaseLogosAsync(viewModels, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeOnDispatcherAsync(
                    () => SetStatus(BuildModDatabaseResultsMessage(hasSearchTokens, viewModels.Count), false),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Intentionally ignored.
        }
        catch (Exception ex)
        {
            await InvokeOnDispatcherAsync(
                    () => SetStatus(BuildModDatabaseErrorMessage(hasSearchTokens, ex.Message), true),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_modDatabaseSearchCts, cts))
            {
                _modDatabaseSearchCts = null;
            }
        }
    }

    private IReadOnlyList<ModDatabaseSearchResult> ApplyModDatabaseFilters(
        IReadOnlyList<ModDatabaseSearchResult> results,
        int maxResults)
    {
        if (!ShowRecentlyUpdatedOnly)
        {
            return results;
        }

        DateTime threshold = DateTime.UtcNow.AddMonths(-3);
        int limit = Math.Max(0, maxResults);

        return results
            .Where(result => result.LastReleasedUtc.HasValue && result.LastReleasedUtc.Value >= threshold)
            .OrderByDescending(result => result.LastReleasedUtc)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private string BuildModDatabaseResultsMessage(bool hasSearchTokens, int resultCount)
    {
        if (hasSearchTokens)
        {
            return ShowRecentlyUpdatedOnly
                ? $"Found {resultCount} recently updated mods in the mod database."
                : $"Found {resultCount} mods in the mod database.";
        }

        if (ShowRecentlyUpdatedOnly)
        {
            return $"Showing {resultCount} recently updated popular mods.";
        }

        if (ShowPopularRecently)
        {
            return $"Showing {resultCount} recently popular mods.";
        }

        return $"Showing {resultCount} of the most downloaded mods.";
    }

    private string BuildNoModDatabaseResultsMessage(bool hasSearchTokens)
    {
        if (hasSearchTokens)
        {
            return ShowRecentlyUpdatedOnly
                ? "No recently updated mods found in the mod database."
                : "No mods found in the mod database.";
        }

        if (ShowRecentlyUpdatedOnly)
        {
            return "No recently updated popular mods were found.";
        }

        if (ShowPopularRecently)
        {
            return "No recently popular mods were found.";
        }

        return "No mods found in the mod database.";
    }

    private string BuildModDatabaseErrorMessage(bool hasSearchTokens, string errorMessage)
    {
        string operation;
        if (hasSearchTokens)
        {
            operation = ShowRecentlyUpdatedOnly
                ? "search for recently updated mods in the mod database"
                : "search the mod database";
        }
        else if (ShowRecentlyUpdatedOnly)
        {
            operation = "load recently updated mods from the mod database";
        }
        else if (ShowPopularRecently)
        {
            operation = "load recently popular mods from the mod database";
        }
        else
        {
            operation = "load popular mods from the mod database";
        }

        return $"Failed to {operation}: {errorMessage}";
    }

    private Task UpdateSearchResultsAsync(IReadOnlyList<ModListItemViewModel> items, CancellationToken cancellationToken)
    {
        return InvokeOnDispatcherAsync(() =>
        {
            _searchResults.Clear();
            foreach (var item in items)
            {
                _searchResults.Add(item);
            }

            SelectedMod = null;
        }, cancellationToken);
    }

    private async Task LoadModDatabaseLogosAsync(IReadOnlyList<ModListItemViewModel> viewModels, CancellationToken cancellationToken)
    {
        if (viewModels.Count == 0)
        {
            return;
        }

        var tasks = new List<Task>(viewModels.Count);
        foreach (ModListItemViewModel viewModel in viewModels)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            tasks.Add(viewModel.LoadModDatabaseLogoAsync(cancellationToken));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Swallow cancellation so callers can continue gracefully.
        }
    }

    private Task<HashSet<string>> GetInstalledModIdsAsync(CancellationToken cancellationToken)
    {
        return InvokeOnDispatcherAsync(
            () =>
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in _mods)
                {
                    if (!string.IsNullOrWhiteSpace(mod.ModId))
                    {
                        installed.Add(mod.ModId);
                    }
                }

                return installed;
            },
            cancellationToken);
    }

    private static bool IsResultInstalled(ModDatabaseSearchResult result, HashSet<string> installedModIds)
    {
        if (installedModIds.Contains(result.ModId))
        {
            return true;
        }

        foreach (string alternate in result.AlternateIds)
        {
            if (!string.IsNullOrWhiteSpace(alternate) && installedModIds.Contains(alternate))
            {
                return true;
            }
        }

        return false;
    }

    private static Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).Task;
        }

        action();
        return Task.CompletedTask;
    }

    private static Task<T> InvokeOnDispatcherAsync<T>(Func<T> function, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                return Task.FromResult(function());
            }

            return dispatcher.InvokeAsync(function, DispatcherPriority.Normal, cancellationToken).Task;
        }

        return Task.FromResult(function());
    }

    private static ModEntry CreateSearchResultEntry(ModDatabaseSearchResult result)
    {
        var authors = string.IsNullOrWhiteSpace(result.Author)
            ? Array.Empty<string>()
            : new[] { result.Author };

        string? description = BuildSearchResultDescription(result);
        string? pageUrl = BuildModDatabasePageUrl(result);

        ModDatabaseInfo databaseInfo = result.DetailedInfo ?? new ModDatabaseInfo
        {
            Tags = result.Tags,
            AssetId = result.AssetId,
            ModPageUrl = pageUrl,
            Downloads = result.Downloads,
            Comments = result.Comments,
            Follows = result.Follows,
            TrendingPoints = result.TrendingPoints,
            LogoUrl = result.LogoUrl,
            LastReleasedUtc = result.LastReleasedUtc
        };

        return new ModEntry
        {
            ModId = result.ModId,
            Name = result.Name,
            Description = description,
            Authors = authors,
            Website = pageUrl,
            SourceKind = ModSourceKind.SourceCode,
            SourcePath = string.Empty,
            Side = result.Side,
            ModDatabaseSearchScore = result.Score,
            DatabaseInfo = databaseInfo
        };
    }

    private ModListItemViewModel CreateSearchResultViewModel(ModEntry entry, bool isInstalled)
    {
        return new ModListItemViewModel(entry, false, "Mod Database", RejectActivationChangeAsync, _installedGameVersion, isInstalled);
    }

    private static string? BuildSearchResultDescription(ModDatabaseSearchResult result)
    {
        return string.IsNullOrWhiteSpace(result.Summary) ? null : result.Summary.Trim();
    }

    private static string? BuildModDatabasePageUrl(ModDatabaseSearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.AssetId))
        {
            return $"https://mods.vintagestory.at/show/mod/{result.AssetId}";
        }

        if (!string.IsNullOrWhiteSpace(result.UrlAlias))
        {
            string alias = result.UrlAlias!.TrimStart('/');
            return string.IsNullOrWhiteSpace(alias) ? null : $"https://mods.vintagestory.at/{alias}";
        }

        return null;
    }

    private static Task<ActivationResult> RejectActivationChangeAsync(ModListItemViewModel mod, bool isActive)
    {
        return Task.FromResult(new ActivationResult(false, "Install this mod locally to manage its activation state."));
    }

    private bool FilterMod(object? item)
    {
        if (item is not ModListItemViewModel mod)
        {
            return false;
        }

        if (_searchTokens.Length == 0)
        {
            return true;
        }

        return mod.MatchesSearchTokens(_searchTokens);
    }

    private static string[] CreateSearchTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
        StatusLogService.AppendStatus(message, isError);
        StatusMessage = message;
        IsErrorStatus = isError;
    }

    private Dictionary<string, ModEntry?> LoadChangedModEntries(IReadOnlyCollection<string> paths)
    {
        var results = new Dictionary<string, ModEntry?>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            ModEntry? entry = _discoveryService.LoadModFromPath(path);
            results[path] = entry;
        }

        return results;
    }

    private void ApplyFullReload(IReadOnlyList<ModEntry> entries, string? previousSelection)
    {
        _modEntriesBySourcePath.Clear();
        _modViewModelsBySourcePath.Clear();
        _mods.Clear();

        foreach (var entry in entries)
        {
            _modEntriesBySourcePath[entry.SourcePath] = entry;
            var viewModel = CreateModViewModel(entry);
            _modViewModelsBySourcePath[entry.SourcePath] = viewModel;
            _mods.Add(viewModel);
        }

        TotalMods = _mods.Count;

        if (!string.IsNullOrWhiteSpace(previousSelection)
            && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selected))
        {
            SelectedMod = selected;
        }
        else
        {
            SelectedMod = null;
        }
    }

    private void ApplyPartialUpdates(IReadOnlyDictionary<string, ModEntry?> changes, string? previousSelection)
    {
        if (changes.Count == 0)
        {
            SetStatus("Mods are up to date.", false);
            if (!string.IsNullOrWhiteSpace(previousSelection)
                && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selected))
            {
                SelectedMod = selected;
            }

            return;
        }

        int added = 0;
        int updated = 0;
        int removed = 0;

        foreach (var change in changes)
        {
            string path = change.Key;
            ModEntry? entry = change.Value;

            if (entry == null)
            {
                if (_modViewModelsBySourcePath.TryGetValue(path, out var existingVm))
                {
                    _mods.Remove(existingVm);
                    _modViewModelsBySourcePath.Remove(path);
                    removed++;

                    if (ReferenceEquals(SelectedMod, existingVm))
                    {
                        SelectedMod = null;
                    }
                }

                _modEntriesBySourcePath.Remove(path);
                continue;
            }

            var viewModel = CreateModViewModel(entry);
            if (_modViewModelsBySourcePath.TryGetValue(path, out var existing))
            {
                int index = _mods.IndexOf(existing);
                if (index >= 0)
                {
                    _mods[index] = viewModel;
                }
                else
                {
                    _mods.Add(viewModel);
                }

                _modViewModelsBySourcePath[path] = viewModel;
                updated++;

                if (ReferenceEquals(SelectedMod, existing))
                {
                    SelectedMod = viewModel;
                }
            }
            else
            {
                _mods.Add(viewModel);
                _modViewModelsBySourcePath[path] = viewModel;
                _modEntriesBySourcePath[path] = entry;
                added++;
            }
        }

        foreach (var pair in _modViewModelsBySourcePath)
        {
            if (changes.ContainsKey(pair.Key))
            {
                continue;
            }

            if (_modEntriesBySourcePath.TryGetValue(pair.Key, out var entry))
            {
                pair.Value.UpdateLoadError(entry.LoadError);
                pair.Value.UpdateDependencyIssues(entry.DependencyHasErrors, entry.MissingDependencies);
            }
        }

        if (!string.IsNullOrWhiteSpace(previousSelection)
            && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selectedAfter))
        {
            SelectedMod = selectedAfter;
        }
        else if (SelectedMod != null && !_mods.Contains(SelectedMod))
        {
            SelectedMod = null;
        }

        int affected = added + updated + removed;
        if (affected == 0)
        {
            SetStatus("Mods are up to date.", false);
            return;
        }

        var parts = new List<string>();
        if (added > 0)
        {
            parts.Add($"{added} added");
        }

        if (updated > 0)
        {
            parts.Add($"{updated} updated");
        }

        if (removed > 0)
        {
            parts.Add($"{removed} removed");
        }

        string summary = parts.Count == 0 ? $"{affected} changed" : string.Join(", ", parts);
        SetStatus($"Applied changes to mods ({summary}).", false);
    }

    private void QueueDatabaseInfoRefresh(IEnumerable<ModEntry> entries)
    {
        if (entries is null)
        {
            return;
        }

        ModEntry[] pending = entries
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.ModId))
            .ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (ModEntry entry in pending)
                {
                    ModDatabaseInfo? info;
                    try
                    {
                        info = await _databaseService
                            .TryLoadDatabaseInfoAsync(entry.ModId, entry.Version, _installedGameVersion)
                            .ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (info is null)
                    {
                        continue;
                    }

                    try
                    {
                        await InvokeOnDispatcherAsync(
                                () =>
                                {
                                    if (_modEntriesBySourcePath.TryGetValue(entry.SourcePath, out var currentEntry)
                                        && ReferenceEquals(currentEntry, entry))
                                    {
                                        currentEntry.UpdateDatabaseInfo(info);

                                        if (_modViewModelsBySourcePath.TryGetValue(entry.SourcePath, out var viewModel))
                                        {
                                            viewModel.UpdateDatabaseInfo(info);
                                        }
                                    }
                                },
                                CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Ignore dispatcher failures to keep refresh resilient.
                    }
                }
            }
            catch (Exception)
            {
                // Swallow unexpected exceptions from the refresh loop.
            }
        });
    }
}
