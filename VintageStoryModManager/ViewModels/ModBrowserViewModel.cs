using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
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
    private CancellationTokenSource? _searchCts;
    private const int DefaultLoadedMods = 45;
    private const int LoadMoreCount = 10;

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<DownloadableModOnList> _modsList = [];

    [ObservableProperty]
    private int _visibleModsCount = DefaultLoadedMods;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _textFilter = string.Empty;

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

        // Load saved settings if available
        if (_userConfigService != null)
        {
            _orderBy = _userConfigService.ModBrowserOrderBy;
            _orderByDirection = _userConfigService.ModBrowserOrderByDirection;
            _selectedSide = _userConfigService.ModBrowserSelectedSide;
            _selectedInstalledFilter = _userConfigService.ModBrowserSelectedInstalledFilter;
            _onlyFavorites = _userConfigService.ModBrowserOnlyFavorites;
        }

        // Subscribe to collection changes for multi-select filters
        SelectedVersions.CollectionChanged += OnFilterCollectionChanged;
        SelectedTags.CollectionChanged += OnFilterCollectionChanged;
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
                    var versionIds = SelectedVersions.Select(v => v.TagId).ToList();
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
                cancellationToken: token);

            if (token.IsCancellationRequested)
                return;

            // Apply client-side filters
            var filteredMods = ApplyClientSideFilters(mods);

            ModsList.Clear();
            foreach (var mod in filteredMods)
            {
                ModsList.Add(mod);
            }

            VisibleModsCount = DefaultLoadedMods;
            OnPropertyChanged(nameof(VisibleMods));
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
    private void LoadMore()
    {
        VisibleModsCount = Math.Min(VisibleModsCount + LoadMoreCount, ModsList.Count);
        OnPropertyChanged(nameof(VisibleMods));
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
                var version = AvailableVersions.FirstOrDefault(v => v.TagId == versionId);
                if (version != null && !SelectedVersions.Contains(version))
                {
                    SelectedVersions.Add(version);
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
        var versions = await _modApiService.GetGameVersionsAsync();
        AvailableVersions.Clear();
        foreach (var version in versions)
        {
            AvailableVersions.Add(version);
        }
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

        // Favorites filter
        if (OnlyFavorites)
        {
            filtered = filtered.Where(m => FavoriteMods.Contains(m.ModId));
        }

        return filtered.ToList();
    }

    #endregion

    #region Property Changed Handlers

    partial void OnTextFilterChanged(string value) => _ = SearchModsAsync();
    partial void OnSelectedAuthorChanged(ModAuthor? value) => _ = SearchModsAsync();
    partial void OnSelectedSideChanged(string value)
    {
        _userConfigService?.SetModBrowserSelectedSide(value);
        _ = SearchModsAsync();
    }
    partial void OnSelectedInstalledFilterChanged(string value)
    {
        _userConfigService?.SetModBrowserSelectedInstalledFilter(value);
        _ = SearchModsAsync();
    }
    partial void OnOnlyFavoritesChanged(bool value)
    {
        _userConfigService?.SetModBrowserOnlyFavorites(value);
        _ = SearchModsAsync();
    }
    partial void OnOrderByChanged(string value)
    {
        _userConfigService?.SetModBrowserOrderBy(value);
        _ = SearchModsAsync();
    }
    partial void OnOrderByDirectionChanged(string value)
    {
        _userConfigService?.SetModBrowserOrderByDirection(value);
        _ = SearchModsAsync();
    }

    #endregion
}

/// <summary>
/// Represents an order by option with icon.
/// </summary>
public record OrderByOption(string Key, string Display, string Icon);
