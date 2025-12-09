using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
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
    private readonly HashSet<string> _normalizedInstalledModIds = new(StringComparer.OrdinalIgnoreCase);
    
    // Cache for raw API results to avoid unnecessary server queries
    private List<DownloadableModOnList> _cachedApiResults = [];

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

            // Versions and tags require server query
            await QueryAndApplyFiltersAsync(forceServerQuery: true);
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
    public bool IsModInstalled(int modId)
    {
        return IsModInstalledById(modId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Updates the installed mod cache using the provided identifiers.
    /// </summary>
    /// <param name="modIds">A collection of mod identifiers to normalize and track.</param>
    /// <param name="numericModIds">Optional numeric identifiers for compatibility with existing bindings.</param>
    public void UpdateInstalledMods(IEnumerable<string> modIds, IEnumerable<int>? numericModIds = null)
    {
        _normalizedInstalledModIds.Clear();
        foreach (var modId in modIds)
        {
            AddInstalledModId(modId);
        }

        InstalledMods.Clear();
        if (numericModIds != null)
        {
            foreach (var modId in numericModIds.Distinct())
                InstalledMods.Add(modId);
        }

        RefreshInstalledFlags();
    }

    /// <summary>
    /// Adds a single installed mod identifier to the cache and updates the UI state.
    /// </summary>
    /// <param name="modId">The mod identifier to add.</param>
    /// <param name="numericModId">Optional numeric identifier to keep the installed list in sync.</param>
    public void AddInstalledMod(string modId, int? numericModId = null)
    {
        AddInstalledModId(modId);

        if (numericModId.HasValue && !InstalledMods.Contains(numericModId.Value))
            InstalledMods.Add(numericModId.Value);

        RefreshInstalledFlags();
    }

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

    public Task RefreshSearchAsync()
    {
        return SearchModsAsync();
    }

    [RelayCommand]
    private async Task SearchModsAsync()
    {
        await QueryAndApplyFiltersAsync(forceServerQuery: false);
    }

    /// <summary>
    /// Main search method that handles both server-side queries and client-side filtering.
    /// </summary>
    /// <param name="forceServerQuery">If true, always queries the server. If false, uses cached results when possible.</param>
    private async Task QueryAndApplyFiltersAsync(bool forceServerQuery)
    {
        // Cancel any pending search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // Determine if we need to query the server or can use cached results
            bool needsServerQuery = forceServerQuery || _cachedApiResults.Count == 0;

            // Only show loading indicator for server queries
            if (needsServerQuery)
            {
                IsSearching = true;
                
                // Add a small delay to debounce rapid typing (only for server queries)
                await Task.Delay(300, token);

                if (token.IsCancellationRequested)
                    return;

                // Query server with filters that must be server-side
                // Note: We now exclude OrderBy and OrderByDirection from server query
                // since we'll sort client-side
                var mods = await _modApiService.QueryModsAsync(
                    textFilter: TextFilter,
                    authorFilter: SelectedAuthor,
                    versionsFilter: SelectedVersions,
                    tagsFilter: SelectedTags,
                    orderBy: "follows", // Use a default for server query
                    orderByOrder: "desc",
                    cancellationToken: token);

                if (token.IsCancellationRequested)
                    return;

                // Cache the raw results
                _cachedApiResults = mods;
                
                // Clear user reports only when we get new data from server
                _userReportsLoaded.Clear();
            }

            // Apply client-side filters and sorting (fast, no loading indicator needed)
            var filteredAndSortedMods = ApplyClientSideFiltersAndSorting(_cachedApiResults);

            ModsList.Clear();
            foreach (var mod in filteredAndSortedMods)
            {
                ModsList.Add(mod);
            }

            VisibleModsCount = DefaultLoadedMods;
            OnPropertyChanged(nameof(VisibleMods));

            // Only populate user reports for visible mods that don't have them yet
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
            AddInstalledMod(mod.ModIdStr ?? mod.ModId.ToString(CultureInfo.InvariantCulture), modId);
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

    private bool IsModInstalled(DownloadableModOnList mod)
    {
        foreach (var candidate in GetCandidateModIds(mod))
        {
            if (IsModInstalledById(candidate))
                return true;
        }

        return false;
    }

    private bool IsModInstalledById(string? modId)
    {
        var normalized = NormalizeModId(modId);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        return _normalizedInstalledModIds.Contains(normalized);
    }

    private void AddInstalledModId(string? modId)
    {
        var normalized = NormalizeModId(modId);
        if (string.IsNullOrWhiteSpace(normalized)) return;

        _normalizedInstalledModIds.Add(normalized);
    }

    private void RefreshInstalledFlags()
    {
        foreach (var mod in ModsList)
        {
            mod.IsInstalled = IsModInstalled(mod);
        }
    }

    private static IEnumerable<string> GetCandidateModIds(DownloadableModOnList mod)
    {
        if (mod.ModId > 0)
            yield return mod.ModId.ToString(CultureInfo.InvariantCulture);

        if (mod.ModIdStrings is { Count: > 0 })
        {
            foreach (var id in mod.ModIdStrings)
            {
                if (!string.IsNullOrWhiteSpace(id)) yield return id;
            }
        }

        if (!string.IsNullOrWhiteSpace(mod.Name))
            yield return mod.Name;
    }

    private static string? NormalizeModId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                if (builder.Length == 0) builder.Append('m');
                builder.Append(ch);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private List<DownloadableModOnList> ApplyClientSideFiltersAndSorting(List<DownloadableModOnList> mods)
    {
        foreach (var mod in mods)
        {
            mod.IsInstalled = IsModInstalled(mod);
        }

        var filtered = mods.AsEnumerable();

        // Side filter
        if (SelectedSide != "any")
        {
            filtered = filtered.Where(m => m.Side.Equals(SelectedSide, StringComparison.OrdinalIgnoreCase));
        }

        // Installed filter
        if (SelectedInstalledFilter == "installed")
        {
            filtered = filtered.Where(m => m.IsInstalled);
        }
        else if (SelectedInstalledFilter == "not-installed")
        {
            filtered = filtered.Where(m => !m.IsInstalled);
        }

        // Favorites filter
        if (OnlyFavorites)
        {
            filtered = filtered.Where(m => FavoriteMods.Contains(m.ModId));
        }

        // Apply client-side sorting based on OrderBy
        filtered = ApplySorting(filtered, OrderBy, OrderByDirection);

        return filtered.ToList();
    }

    private IEnumerable<DownloadableModOnList> ApplySorting(IEnumerable<DownloadableModOnList> mods, string orderBy, string direction)
    {
        bool ascending = direction == "asc";

        var sorted = orderBy switch
        {
            "downloads" => ascending 
                ? mods.OrderBy(m => m.Downloads) 
                : mods.OrderByDescending(m => m.Downloads),
            "comments" => ascending 
                ? mods.OrderBy(m => m.Comments) 
                : mods.OrderByDescending(m => m.Comments),
            "follows" => ascending 
                ? mods.OrderBy(m => m.Follows) 
                : mods.OrderByDescending(m => m.Follows),
            "trendingpoints" => ascending 
                ? mods.OrderBy(m => m.TrendingPoints) 
                : mods.OrderByDescending(m => m.TrendingPoints),
            "lastreleased" => ascending 
                ? mods.OrderBy(m => m.LastReleased) 
                : mods.OrderByDescending(m => m.LastReleased),
            "asset.created" => ascending 
                ? mods.OrderBy(m => m.LastReleased) // Use LastReleased as fallback
                : mods.OrderByDescending(m => m.LastReleased),
            _ => ascending 
                ? mods.OrderBy(m => m.Follows) 
                : mods.OrderByDescending(m => m.Follows)
        };

        return sorted;
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
            mod.ShowUserReportBadge = false;
            mod.UserReportDisplay = string.Empty;
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
                mod.ShowUserReportBadge = false;
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
            mod.ShowUserReportBadge = false;
            mod.UserReportDisplay = string.Empty;
            mod.UserReportTooltip = "User reports require a known Vintage Story version.";
            return;
        }

        var modDetails = await _modApiService.GetModAsync(mod.ModId, cancellationToken);
        if (modDetails?.Releases is null || modDetails.Releases.Count == 0)
        {
            mod.ShowUserReportBadge = false;
            mod.UserReportDisplay = string.Empty;
            mod.UserReportTooltip = "No releases available to load user reports.";
            return;
        }

        var latestRelease = modDetails.Releases
            .OrderByDescending(r => DateTime.TryParse(r.Created, out var date) ? date : DateTime.MinValue)
            .FirstOrDefault();

        if (latestRelease is null || string.IsNullOrWhiteSpace(latestRelease.ModVersion))
        {
            mod.ShowUserReportBadge = false;
            mod.UserReportDisplay = string.Empty;
            mod.UserReportTooltip = "Latest release version is unavailable for this mod.";
            return;
        }

        var summary = await _voteService.GetVoteSummaryAsync(
            mod.ModId.ToString(CultureInfo.InvariantCulture),
            latestRelease.ModVersion,
            _installedGameVersion,
            cancellationToken);

        var display = BuildUserReportDisplay(summary);
        mod.ShowUserReportBadge = !string.IsNullOrWhiteSpace(display);
        mod.UserReportDisplay = display ?? string.Empty;
        mod.UserReportTooltip = BuildUserReportTooltip(summary, _installedGameVersion);
    }

    private static string? BuildUserReportDisplay(ModVersionVoteSummary? summary)
    {
        if (summary is null || summary.TotalVotes == 0) return null;

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
        if (summary is null || summary.TotalVotes == 0)
        {
            return string.IsNullOrWhiteSpace(vintageStoryVersion)
                ? "No user reports yet. Click to share your experience."
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "No user reports yet for Vintage Story {0}. Click to share your experience.",
                    vintageStoryVersion);
        }

        var counts = summary.Counts;
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
        {
            // Text filter requires server query
            _ = QueryAndApplyFiltersAsync(forceServerQuery: true);
        }
    }

    partial void OnSelectedAuthorChanged(ModAuthor? value)
    {
        if (!_isInitializing)
        {
            // Author filter requires server query
            _ = QueryAndApplyFiltersAsync(forceServerQuery: true);
        }
    }

    partial void OnSelectedSideChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserSelectedSide(value);
            // Client-side filter - no need to query server
            _ = QueryAndApplyFiltersAsync(forceServerQuery: false);
        }
    }

    partial void OnSelectedInstalledFilterChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserSelectedInstalledFilter(value);
            // Client-side filter - no need to query server
            _ = QueryAndApplyFiltersAsync(forceServerQuery: false);
        }
    }

    partial void OnOnlyFavoritesChanged(bool value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOnlyFavorites(value);
            // Client-side filter - no need to query server
            _ = QueryAndApplyFiltersAsync(forceServerQuery: false);
        }
    }

    partial void OnOrderByChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOrderBy(value);
            // Client-side sort - no need to query server
            _ = QueryAndApplyFiltersAsync(forceServerQuery: false);
        }
    }

    partial void OnOrderByDirectionChanged(string value)
    {
        if (!_isInitializing)
        {
            _userConfigService?.SetModBrowserOrderByDirection(value);
            // Client-side sort - no need to query server
            _ = QueryAndApplyFiltersAsync(forceServerQuery: false);
        }
    }

    #endregion
}

/// <summary>
/// Represents an order by option with icon.
/// </summary>
public record OrderByOption(string Key, string Display, string Icon);
