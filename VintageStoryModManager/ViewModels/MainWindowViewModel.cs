using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ObservableCollection<ModViewModel> _mods;
    private readonly ClientSettingsStore? _settingsStore;
    private readonly ModDiscoveryService? _modDiscoveryService;
    private SortOptionViewModel _selectedSortOption;
    private bool _sortDescending;
    private string _statusMessage = string.Empty;
    private string _summary = string.Empty;

    public MainWindowViewModel()
    {
        _mods = new ObservableCollection<ModViewModel>();
        Mods = new ReadOnlyObservableCollection<ModViewModel>(_mods);
        SortOptions = new ObservableCollection<SortOptionViewModel>(SortOptionViewModel.CreateDefaults());
        _selectedSortOption = SortOptions.First();
        Summary = "No mods loaded.";
        StatusMessage = "Ready";
        RefreshCommand = new RelayCommand(() => { });
    }

    public MainWindowViewModel(ModDiscoveryService modDiscoveryService, ClientSettingsStore settingsStore)
    {
        _modDiscoveryService = modDiscoveryService ?? throw new ArgumentNullException(nameof(modDiscoveryService));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _mods = new ObservableCollection<ModViewModel>();
        Mods = new ReadOnlyObservableCollection<ModViewModel>(_mods);
        SortOptions = new ObservableCollection<SortOptionViewModel>(SortOptionViewModel.CreateDefaults());
        _selectedSortOption = SortOptions.First();
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public ReadOnlyObservableCollection<ModViewModel> Mods { get; }

    public ObservableCollection<SortOptionViewModel> SortOptions { get; }

    public RelayCommand RefreshCommand { get; }

    public SortOptionViewModel SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplySort();
            }
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (SetProperty(ref _sortDescending, value))
            {
                ApplySort();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    private void Refresh()
    {
        if (_settingsStore == null || _modDiscoveryService == null)
        {
            return;
        }

        foreach (var mod in _mods)
        {
            mod.ActiveStateChanged -= OnModActiveStateChanged;
        }

        _mods.Clear();

        var entries = _modDiscoveryService.LoadMods();
        foreach (var entry in entries)
        {
            string? versionKey = !string.IsNullOrWhiteSpace(entry.Version) ? entry.Version : (!string.IsNullOrWhiteSpace(entry.NetworkVersion) ? entry.NetworkVersion : null);
            bool isActive = !_settingsStore.IsDisabled(entry.ModId, versionKey);
            var vm = new ModViewModel(entry, isActive, _settingsStore, ReportStatus);
            vm.ActiveStateChanged += OnModActiveStateChanged;
            _mods.Add(vm);
        }

        ApplySort();
        UpdateSummary();
        StatusMessage = $"Loaded {entries.Count} mods.";
    }

    private void ApplySort()
    {
        if (_mods.Count <= 1)
        {
            return;
        }

        IOrderedEnumerable<ModViewModel> ordered = SelectedSortOption.Key switch
        {
            SortKey.Name => _mods.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortKey.Status => _mods.OrderByDescending(m => m.IsActive).ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => _mods.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
        };

        var sorted = ordered.ToList();
        if (SortDescending)
        {
            sorted.Reverse();
        }

        _mods.Clear();
        foreach (var mod in sorted)
        {
            _mods.Add(mod);
        }
    }

    private void OnModActiveStateChanged(object? sender, EventArgs e)
    {
        UpdateSummary();
        ApplySort();
    }

    private void UpdateSummary()
    {
        int total = _mods.Count;
        int active = _mods.Count(m => m.IsActive);
        Summary = total == 0 ? "No mods detected." : $"{active} of {total} mods active";
    }

    private void ReportStatus(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusMessage = message!;
        }

        UpdateSummary();
    }
}

public sealed class SortOptionViewModel
{
    public SortOptionViewModel(SortKey key, string display)
    {
        Key = key;
        Display = display;
    }

    public SortKey Key { get; }

    public string Display { get; }

    public static SortOptionViewModel[] CreateDefaults() => new[]
    {
        new SortOptionViewModel(SortKey.Name, "Name"),
        new SortOptionViewModel(SortKey.Status, "Active state")
    };

    public override string ToString() => Display;
}

public enum SortKey
{
    Name,
    Status
}
