using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for the main mod browser view.
/// </summary>
public partial class ModBrowserViewModel : ObservableObject
{
    private readonly IModApiService _modApiService;
    private readonly UserConfigurationService? _userConfigService;
    private readonly ModVersionVoteService _voteService;
    private readonly string? _installedGameVersion;
    private CancellationTokenSource? _searchCts;
    private const int DefaultLoadedMods = 45;
    private const int LoadMoreCount = 10;
    private bool _isInitializing;
    private Func<DownloadableMod, Task>? _installModCallback;
    private readonly HashSet<int> _userReportsLoaded = new();

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<DownloadableModOnList> _modsList = [];

    [ObservableProperty]
    private int _visibleModsCount = DefaultLoadedMods;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _textFilter = string.Empty;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(TextFilter);

    [ObservableProperty]
    private ModAuthor? _selectedAuthor;

    [ObservableProperty]
    private ObservableCollection<ModAuthor> _availableAuthors = [];

    [ObservableProperty]
    private ObservableCollection<GameVersion> _selectedVersions = [];

    [ObservableProperty]
    private ObservableCollection<GameVersion> _availableVersions = [];

    [ObservableProperty]
    private ObservableCollection<ModTag> _selectedTags = [];

    [ObservableProperty]
    private ObservableCollection<ModTag> _availableTags = [];

    [ObservableProperty]
    private string _selectedSide = "any";

    [ObservableProperty]
    private string _selectedInstalledFilter = "all";

    [ObservableProperty]
    private bool _onlyFavorites;

    [ObservableProperty]
    private string _orderBy = "follows";

    [ObservableProperty]
    private string _orderByDirection = "desc";

    [ObservableProperty]
    private DownloadableModOnList? _selectedMod;

    [ObservableProperty]
    private DownloadableMod? _modToInstall;

    [ObservableProperty]
    private bool _isInstallDialogOpen;

    [ObservableProperty]
    private ObservableCollection<int> _favoriteMods = [];

    [ObservableProperty]
    private ObservableCollection<int> _installedMods = [];

    #endregion

    #region Filter Options

    public static List<KeyValuePair<string, string>> SideOptions =>
    [
        new("any", "Any"),
        new("both", "Both"),
        new("server", "Server"),
        new("client", "Client")
    ];

    public static List<KeyValuePair<string, string>> InstalledFilterOptions =>
    [
        new("all", "All"),
        new("installed", "Installed"),
        new("not-installed", "Not Installed")
    ];

    public static List<OrderByOption> OrderByOptions =>
    [
        new("trendingpoints", "Trending", "IconFire"),
        new("downloads", "Downloads", "IconDownload"),
        new("comments", "Comments", "IconMessage"),
        new("lastreleased", "Updated", "IconHistory"),
        new("asset.created", "Created", "IconCalendar"),
        new("follows", "Follows", "IconHeartSolid")
    ];

    #endregion

    public ModBrowserViewModel(IModApiService modApiService, UserConfigurationService? userConfigService = null)
    {
        _modApiService = modApiService;
        _userConfigService = userConfigService;
        _voteService = new ModVersionVoteService();
        _installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion(_userConfigService?.GameDirectory);
        _isInitializing = true;

        // Subscribe to collection changes for multi-select filters
        SelectedVersions.CollectionChanged += OnFilterCollectionChanged;
        SelectedTags.CollectionChanged += OnFilterCollectionChanged;

        // Load saved settings if available
        // Use public properties to ensure PropertyChanged events are raised
        if (_userConfigService != null)
        {
            OrderBy = _userConfigService.ModBrowserOrderBy;
            OrderByDirection = _userConfigService.ModBrowserOrderByDirection;
            SelectedSide = _userConfigService.ModBrowserSelectedSide;
            SelectedInstalledFilter = _userConfigService.ModBrowserSelectedInstalledFilter;
            OnlyFavorites = _userConfigService.ModBrowserOnlyFavorites;
        }

        _isInitializing = false;
    }

    /// <summary>
    /// Sets the callback to be invoked when a mod needs to be installed.
    /// </summary>
    public void SetInstallModCallback(Func<DownloadableMod, Task> callback)
    {
        _installModCallback = callback;
    }

    private async void OnFilterCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            // Save selections to config
            if (_userConfigService != null)
            {
                if (sender == SelectedVersions)
                {
                    var versionIds = SelectedVersions.Select(v => v.TagId.ToString()).ToList();
                    _userConfigService.SetModBrowserSelectedVersionIds(versionIds);
                }
                else if (sender == SelectedTags)
                {
                    var tagIds = SelectedTags.Select(t => t.TagId).ToList();
                    _userConfigService.SetModBrowserSelectedTagIds(tagIds);
                }
            }

            await SearchModsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during filter search: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the visible mods based on pagination.
    /// </summary>
    public IEnumerable<DownloadableModOnList> VisibleMods =>
        ModsList.Take(VisibleModsCount);


    /// <summary>
    /// Checks if a mod is a favorite.
    /// </summary>
    public bool IsModFavorite(int modId) => FavoriteMods.Contains(modId);

    /// <summary>
    /// Checks if a mod is installed.
    /// </summary>
    public bool IsModInstalled(int modId) => InstalledMods.Contains(modId);

    #region Commands

    [RelayCommand]
    private void GoBack()
    {
        // In a full implementation, this would navigate back or close the view
        // For the standalone browser, we'll just close the window
        System.Windows.Application.Current?.MainWindow?.Close();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await Task.WhenAll(
            LoadAuthorsAsync(),
            LoadGameVersionsAsync(),
            LoadTagsAsync()
        );

        // Restore previously selected versions and tags after loading available options
        RestoreSavedSelections();

        await SearchModsAsync();
    }

    [RelayCommand]
    private async Task SearchModsAsync()
    {
        // Cancel any pending search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            IsSearching = true;

            // Add a small delay to debounce rapid typing
            await Task.Delay(400, token);

            if (token.IsCancellationRequested)
                return;

            var mods = await _modApiService.QueryModsAsync(
                textFilter: TextFilter,
                authorFilter: SelectedAuthor,
                versionsFilter: SelectedVersions,
                tagsFilter: SelectedTags,
                orderBy: OrderBy,
                orderByOrder: OrderByDirection,
                includeLatestRelease: true,
                cancellationToken: token);

            if (token.IsCancellationRequested)
                return;

            // Apply client-side filters
            var filteredMods = ApplyClientSideFilters(mods);

            ModsList.Clear();
            _userReportsLoaded.Clear(); // Reset user reports tracking
            foreach (var mod in filteredMods)
            {
                ModsList.Add(mod);
            }

            VisibleModsCount = DefaultLoadedMods;
            OnPropertyChanged(nameof(VisibleMods));

            // Only populate user reports for visible mods + one batch ahead
            var modsToLoadReports = ModsList.Take(VisibleModsCount + LoadMoreCount).ToList();
            _ = PopulateUserReportsAsync(modsToLoadReports, token);
        }
        catch (OperationCanceledException)
        {
            // Expected when search is cancelled
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        TextFilter = string.Empty;
    }

    [RelayCommand]
    private void LoadMore()
    {
        var previousCount = VisibleModsCount;
        VisibleModsCount = Math.Min(VisibleModsCount + LoadMoreCount, ModsList.Count);
        OnPropertyChanged(nameof(VisibleMods));

        // Load user reports for newly visible mods + one batch ahead
        var startIndex = previousCount;
        var endIndex = Math.Min(VisibleModsCount + LoadMoreCount, ModsList.Count);
        var modsToLoadReports = ModsList.Skip(startIndex).Take(endIndex - startIndex).ToList();
        
        if (modsToLoadReports.Any())
        {
            _ = PopulateUserReportsAsync(modsToLoadReports, _searchCts?.Token ?? CancellationToken.None);
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        TextFilter = string.Empty;
        SelectedAuthor = null;
        SelectedVersions.Clear();
        SelectedTags.Clear();
        SelectedSide = "any";
        SelectedInstalledFilter = "all";
        OnlyFavorites = false;
    }

    [RelayCommand]
    private void ToggleFavoriteFilter()
    {
        OnlyFavorites = !OnlyFavorites;
    }

    [RelayCommand]
    private async Task OpenModDetailsAsync(DownloadableModOnList mod)
    {
        SelectedMod = mod;
        ModToInstall = await _modApiService.GetModAsync(mod.ModId);
        IsInstallDialogOpen = true;
    }

    [RelayCommand]
    private void CloseInstallDialog()
    {
        IsInstallDialogOpen = false;
        ModToInstall = null;
        SelectedMod = null;
    }

    [RelayCommand]
    private void ToggleFavorite(int modId)
    {
        if (FavoriteMods.Contains(modId))
        {
            FavoriteMods.Remove(modId);
        }
        else
        {
            FavoriteMods.Add(modId);
        }
        // Notify the UI that FavoriteMods has changed so MultiBindings re-evaluate
        OnPropertyChanged(nameof(FavoriteMods));
    }

    [RelayCommand]
    private async Task InstallModAsync(int modId)
    {
        System.Diagnostics.Debug.WriteLine($"[ModBrowser] InstallModAsync called with modId: {modId}");
        
        // Check if internet access is disabled
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            System.Diagnostics.Debug.WriteLine("Cannot install mod: Internet access is disabled");
            // Note: In a full implementation, we'd want to show a user-facing message here
            // but that would require a message service or similar to be passed to the ViewModel
            return;
        }

        // Fetch full mod details
        System.Diagnostics.Debug.WriteLine($"[ModBrowser] Fetching mod details for modId: {modId}");
        var mod = await _modApiService.GetModAsync(modId);
        if (mod == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ModBrowser] Failed to fetch mod details for modId: {modId}");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[ModBrowser] Fetched mod details: {mod.Name}");

        // If a callback is registered, use it (MainWindow will handle the actual installation)
        if (_installModCallback != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ModBrowser] Calling install callback for mod: {mod.Name}");
            await _installModCallback(mod);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ModBrowser] No install callback registered, adding to installed list");
            // Fallback: just mark as installed in the UI
            if (!InstalledMods.Contains(modId))
            {
                InstalledMods.Add(modId);
                OnPropertyChanged(nameof(InstalledMods));
            }
        }
    }

    [RelayCommand]
    private void OpenModInBrowser(int assetId)
    {
        var url = $"https://mods.vintagestory.at/show/mod/{assetId}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ChangeOrderBy(string newOrderBy)
    {
        if (OrderBy == newOrderBy)
        {
            // Toggle direction
            OrderByDirection = OrderByDirection == "desc" ? "asc" : "desc";
        }
        else
        {
            OrderBy = newOrderBy;
            OrderByDirection = "desc";
        }
    }

    [RelayCommand]
    private void ToggleOrderDirection()
    {
        OrderByDirection = OrderByDirection == "desc" ? "asc" : "desc";
    }

    [RelayCommand]
    private void ToggleVersion(GameVersion version)
    {
        if (SelectedVersions.Contains(version))
        {
            SelectedVersions.Remove(version);
        }
        else
        {
            SelectedVersions.Add(version);
        }
    }

    [RelayCommand]
    private void ToggleTag(ModTag tag)
    {
        if (SelectedTags.Contains(tag))
        {
            SelectedTags.Remove(tag);
        }
        else
        {
            SelectedTags.Add(tag);
        }
    }

    #endregion

    #region Private Methods

    private void RestoreSavedSelections()
    {
        if (_userConfigService == null) return;

        // Temporarily unsubscribe to prevent triggering searches while restoring
        SelectedVersions.CollectionChanged -= OnFilterCollectionChanged;
        SelectedTags.CollectionChanged -= OnFilterCollectionChanged;

        try
        {
            // Restore selected versions
            var savedVersionIds = _userConfigService.ModBrowserSelectedVersionIds;
            foreach (var versionId in savedVersionIds)
            {
                if (long.TryParse(versionId, out var tagId))
                {
                    var version = AvailableVersions.FirstOrDefault(v => v.TagId == tagId);
                    if (version != null && !SelectedVersions.Contains(version))
                    {
                        SelectedVersions.Add(version);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ModBrowserViewModel] Failed to parse version ID '{versionId}' as long");
                }
            }

            // Restore selected tags
            var savedTagIds = _userConfigService.ModBrowserSelectedTagIds;
            foreach (var tagId in savedTagIds)
            {
                var tag = AvailableTags.FirstOrDefault(t => t.TagId == tagId);
                if (tag != null && !SelectedTags.Contains(tag))
                {
                    SelectedTags.Add(tag);
                }
            }
        }
        finally
        {
            // Re-subscribe
            SelectedVersions.CollectionChanged += OnFilterCollectionChanged;
            SelectedTags.CollectionChanged += OnFilterCollectionChanged;
        }
    }

    private async Task LoadAuthorsAsync()
    {
        var authors = await _modApiService.GetAuthorsAsync();
        AvailableAuthors.Clear();
        foreach (var author in authors)
        {
            AvailableAuthors.Add(author);
        }
    }

    private async Task LoadGameVersionsAsync()
    {
        System.Diagnostics.Debug.WriteLine("LoadGameVersionsAsync: Starting");
        var versions = await _modApiService.GetGameVersionsAsync();
        System.Diagnostics.Debug.WriteLine($"LoadGameVersionsAsync: Received {versions.Count} versions from API");
        AvailableVersions.Clear();
        System.Diagnostics.Debug.WriteLine($"LoadGameVersionsAsync: Cleared AvailableVersions collection");
        foreach (var version in versions)
        {
            AvailableVersions.Add(version);
        }
        System.Diagnostics.Debug.WriteLine($"LoadGameVersionsAsync: Completed. AvailableVersions now has {AvailableVersions.Count} items");
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _modApiService.GetTagsAsync();
        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            AvailableTags.Add(tag);
        }
    }

    private List<DownloadableModOnList> ApplyClientSideFilters(List<DownloadableModOnList> mods)
    {
        var filtered = mods.AsEnumerable();

        // Side filter
        if (SelectedSide != "any")
        {
            filtered = filtered.Where(m => m.Side.Equals(SelectedSide, StringComparison.OrdinalIgnoreCase));
        }

        // Installed filter
        if (SelectedInstalledFilter == "installed")
        {
            filtered = filtered.Where(m => InstalledMods.Contains(m.ModId));
        }
        else if (SelectedInstalledFilter == "not-installed")
        {
            filtered = filtered.Where(m => !InstalledMods.Contains(m.ModId));
        }

        // Favorites filter
        if (OnlyFavorites)
        {
            filtered = filtered.Where(m => FavoriteMods.Contains(m.ModId));
        }

        return filtered.ToList();
    }

    private async Task PopulateUserReportsAsync(IEnumerable<DownloadableModOnList> mods, CancellationToken cancellationToken)
    {
        // Filter to only mods that haven't had their user reports loaded yet (thread-safe)
        List<DownloadableModOnList> modsToLoad;
        lock (_userReportsLoaded)
        {
            modsToLoad = mods.Where(m => !_userReportsLoaded.Contains(m.ModId)).ToList();
        }
        
        if (modsToLoad.Count == 0)
            return;

        foreach (var mod in modsToLoad)
        {
            mod.UserReportDisplay = "Loading reports…";
            mod.UserReportTooltip = "Fetching user reports for this mod version.";
        }

        // Load user reports in parallel with a concurrency limit
        const int maxConcurrentLoads = 5;
        using var semaphore = new SemaphoreSlim(maxConcurrentLoads);
        var tasks = modsToLoad.Select(async mod =>
        {
            // Check cancellation before acquiring semaphore
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await LoadUserReportAsync(mod, cancellationToken);
                    lock (_userReportsLoaded)
                    {
                        _userReportsLoaded.Add(mod.ModId);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                mod.UserReportDisplay = "Unavailable";
                mod.UserReportTooltip = string.Format(
                    CultureInfo.CurrentCulture,
                    "Failed to load user reports: {0}",
                    ex.Message);
                lock (_userReportsLoaded)
                {
                    _userReportsLoaded.Add(mod.ModId); // Mark as loaded even if failed to avoid retry
                }
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task LoadUserReportAsync(DownloadableModOnList mod, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_installedGameVersion))
        {
            mod.UserReportDisplay = "User reports unavailable";
            mod.UserReportTooltip = "User reports require a known Vintage Story version.";
            return;
        }

        DownloadableModRelease? latestRelease = null;

        if (!string.IsNullOrWhiteSpace(mod.LatestReleaseVersion))
        {
            latestRelease = new DownloadableModRelease
            {
                ModVersion = mod.LatestReleaseVersion!,
                Tags = mod.LatestReleaseTags.ToList()
            };
        }
        else
        {
            latestRelease = await _modApiService.GetLatestReleaseAsync(mod.ModId, cancellationToken);
            if (latestRelease != null)
            {
                mod.LatestReleaseVersion = latestRelease.ModVersion;
                mod.LatestReleaseTags = latestRelease.Tags?.ToList() ?? [];
            }
        }

        if (latestRelease is null || string.IsNullOrWhiteSpace(latestRelease.ModVersion))
        {
            var modDetails = await _modApiService.GetModAsync(mod.ModId, cancellationToken);
            latestRelease = modDetails?.Releases
                .OrderByDescending(r => DateTime.TryParse(r.Created, out var date) ? date : DateTime.MinValue)
                .FirstOrDefault();

            if (latestRelease != null)
            {
                mod.LatestReleaseVersion = latestRelease.ModVersion;
                mod.LatestReleaseTags = latestRelease.Tags?.ToList() ?? [];
            }
        }

        if (latestRelease is null || string.IsNullOrWhiteSpace(latestRelease.ModVersion))
        {
            mod.UserReportDisplay = "User reports unavailable";
            mod.UserReportTooltip = "Latest release version is unavailable for this mod.";
            return;
        }

        var summary = await _voteService.GetVoteSummaryAsync(
            mod.ModId.ToString(CultureInfo.InvariantCulture),
            latestRelease.ModVersion,
            _installedGameVersion,
            cancellationToken);

        mod.UserReportDisplay = BuildUserReportDisplay(summary);
        mod.UserReportTooltip = BuildUserReportTooltip(summary, _installedGameVersion);
    }

    private static string BuildUserReportDisplay(ModVersionVoteSummary? summary)
    {
        if (summary is null || summary.TotalVotes == 0) return "No votes";

        var majority = summary.GetMajorityOption();
        if (majority is null)
        {
            return string.Concat(
                "Mixed (",
                summary.TotalVotes.ToString(CultureInfo.CurrentCulture),
                ")");
        }

        var count = summary.Counts.GetCount(majority.Value);
        var displayName = majority.Value.ToDisplayString();
        return string.Concat(
            displayName,
            " (",
            count.ToString(CultureInfo.CurrentCulture),
            ")");
    }

    private static string BuildUserReportTooltip(ModVersionVoteSummary? summary, string vintageStoryVersion)
    {
        var counts = summary?.Counts ?? ModVersionVoteCounts.Empty;
        var header = string.IsNullOrWhiteSpace(vintageStoryVersion)
            ? "User reports:"
            : string.Format(CultureInfo.CurrentCulture, "User reports for Vintage Story {0}:", vintageStoryVersion);

        return string.Join(Environment.NewLine, new[]
        {
            header,
            string.Format(CultureInfo.CurrentCulture, "Fully functional ({0})", counts.FullyFunctional),
            string.Format(CultureInfo.CurrentCulture, "No issues noticed ({0})", counts.NoIssuesSoFar),
            string.Format(CultureInfo.CurrentCulture, "Some issues but works ({0})", counts.SomeIssuesButWorks),
            string.Format(CultureInfo.CurrentCulture, "Not functional ({0})", counts.NotFunctional),
            string.Format(CultureInfo.CurrentCulture, "Crashes/Freezes game ({0})", counts.CrashesOrFreezesGame)
        });
    }

    #endregion

    #region Property Changed Handlers

    partial void OnTextFilterChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchText));
        if (!_isInitializing)
            _ = SearchModsAsync();
    }

    partial void OnSelectedAuthorChanged(ModAuthor? value)
    {
        if (!_isInitializing)
            _ = SearchModsAsync();
    }

    partial void OnSelectedSideChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserSelectedSide(value);
            _ = SearchModsAsync();
        }
    }

    partial void OnSelectedInstalledFilterChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserSelectedInstalledFilter(value);
            _ = SearchModsAsync();
        }
    }

    partial void OnOnlyFavoritesChanged(bool value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOnlyFavorites(value);
            _ = SearchModsAsync();
        }
    }

    partial void OnOrderByChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOrderBy(value);
            _ = SearchModsAsync();
        }
    }

    partial void OnOrderByDirectionChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOrderByDirection(value);
            _ = SearchModsAsync();
        }
    }

    #endregion
}

/// <summary>
/// Represents an order by option with icon.
/// </summary>
public record OrderByOption(string Key, string Display, string Icon);
