using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using System.Windows.Threading;

using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using ModernWpf.Controls;

using VintageStoryModManager.Services;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using WinForms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{
    private readonly UserConfigurationService _userConfiguration;
    private readonly ObservableCollection<ModPreset> _presets = new();
    private MainViewModel? _viewModel;
    private string? _dataDirectory;
    private string? _gameDirectory;
    private bool _isInitializing;
    private bool _isApplyingPreset;
    private bool _isHoveringSavePresetButton;
    private string? _pendingPresetReselection;

    private DispatcherTimer? _modsWatcherTimer;
    private bool _isAutomaticRefreshRunning;

    private readonly List<ModListItemViewModel> _selectedMods = new();
    private ModListItemViewModel? _selectionAnchor;
    private INotifyCollectionChanged? _modsCollection;
    private bool _isApplyingMultiToggle;


    public MainWindow()
    {
        InitializeComponent();

        _userConfiguration = new UserConfigurationService();
        PresetComboBox.ItemsSource = _presets;
        PresetComboBox.AddHandler(ComboBoxItem.PreviewMouseDownEvent,
            new MouseButtonEventHandler(PresetComboBox_OnItemPreviewMouseDown));
        PresetComboBox.DropDownClosed += PresetComboBox_OnDropDownClosed;
        RefreshPresetList();

        if (!TryInitializePaths())
        {
            WpfApplication.Current?.Shutdown();
            return;
        }

        try
        {
            InitializeViewModel();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to initialize the mod manager:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            WpfApplication.Current?.Shutdown();
            return;
        }

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        if (_viewModel != null)
        {
            await InitializeViewModelAsync(_viewModel);
        }
    }

    private void RefreshPresetList(string? presetToSelect = null)
    {
        string? desired = presetToSelect ?? (PresetComboBox.SelectedItem as ModPreset)?.Name;

        _presets.Clear();
        foreach (var preset in _userConfiguration.GetPresets())
        {
            _presets.Add(preset);
        }

        if (string.IsNullOrWhiteSpace(desired))
        {
            if (PresetComboBox.SelectedItem != null)
            {
                PresetComboBox.SelectedItem = null;
            }

            UpdateSavePresetButtonContent();
            return;
        }

        var match = _presets.FirstOrDefault(preset =>
            string.Equals(preset.Name, desired, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            PresetComboBox.SelectedItem = match;
        }
        else if (PresetComboBox.SelectedItem != null)
        {
            PresetComboBox.SelectedItem = null;
        }

        UpdateSavePresetButtonContent();
    }

    private void InitializeViewModel()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            throw new InvalidOperationException("The data directory is not set.");
        }

        _viewModel = new MainViewModel(_dataDirectory);
        DataContext = _viewModel;
        AttachToModsView(_viewModel.ModsView);
    }

    private async Task InitializeViewModelAsync(MainViewModel viewModel)
    {
        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;
        bool initialized = false;
        try
        {
            await viewModel.InitializeAsync();
            initialized = true;
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to load mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isInitializing = false;
        }

        if (initialized)
        {
            StartModsWatcher();
        }
    }

    private void StartModsWatcher()
    {
        if (_viewModel is null)
        {
            return;
        }

        StopModsWatcher();

        _modsWatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _modsWatcherTimer.Tick += ModsWatcherTimerOnTick;
        _modsWatcherTimer.Start();
    }

    private void StopModsWatcher()
    {
        if (_modsWatcherTimer is null)
        {
            return;
        }

        _modsWatcherTimer.Stop();
        _modsWatcherTimer.Tick -= ModsWatcherTimerOnTick;
        _modsWatcherTimer = null;
        _isAutomaticRefreshRunning = false;
    }

    private async void ModsWatcherTimerOnTick(object? sender, EventArgs e)
    {
        if (_viewModel is null || _viewModel.IsBusy || _isInitializing || _isAutomaticRefreshRunning)
        {
            return;
        }

        bool hasChanges = await _viewModel.CheckForModStateChangesAsync();
        if (!hasChanges)
        {
            return;
        }

        if (_viewModel.RefreshCommand == null)
        {
            return;
        }

        _isAutomaticRefreshRunning = true;
        try
        {
            await _viewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to refresh mods automatically:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isAutomaticRefreshRunning = false;
        }
    }

    private bool TryInitializePaths()
    {
        if (!TryValidateDataDirectory(_userConfiguration.DataDirectory, out _dataDirectory, out _))
        {
            TryValidateDataDirectory(DataDirectoryLocator.Resolve(), out _dataDirectory, out _);
        }

        if (_dataDirectory is null)
        {
            WpfMessageBox.Show("The Vintage Story data folder could not be located. Please select it to continue.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _dataDirectory = PromptForDirectory(
                "Select your VintagestoryData folder",
                _userConfiguration.DataDirectory ?? DataDirectoryLocator.Resolve(),
                TryValidateDataDirectory,
                allowCancel: false);

            if (_dataDirectory is null)
            {
                return false;
            }
        }

        if (!TryValidateGameDirectory(_userConfiguration.GameDirectory, out _gameDirectory, out _))
        {
            TryValidateGameDirectory(GameDirectoryLocator.Resolve(), out _gameDirectory, out _);
        }

        if (_gameDirectory is null)
        {
            WpfMessageBox.Show("The Vintage Story installation folder could not be located. Please select it to continue.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _gameDirectory = PromptForDirectory(
                "Select your Vintage Story installation folder",
                _userConfiguration.GameDirectory ?? GameDirectoryLocator.Resolve(),
                TryValidateGameDirectory,
                allowCancel: false);

            if (_gameDirectory is null)
            {
                return false;
            }
        }

        _userConfiguration.SetDataDirectory(_dataDirectory);
        _userConfiguration.SetGameDirectory(_gameDirectory);
        return true;
    }

    private async Task ReloadViewModelAsync()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            return;
        }

        StopModsWatcher();

        try
        {
            var viewModel = new MainViewModel(_dataDirectory);
            _viewModel = viewModel;
            DataContext = viewModel;
            AttachToModsView(viewModel.ModsView);
            await InitializeViewModelAsync(viewModel);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to reload mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private delegate bool PathValidator(string? path, out string? normalizedPath, out string? errorMessage);

    private string? PromptForDirectory(string description, string? initialPath, PathValidator validator, bool allowCancel)
    {
        string? candidate = initialPath;

        while (true)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                dialog.SelectedPath = candidate;
            }

            WinForms.DialogResult result = dialog.ShowDialog();
            if (result != WinForms.DialogResult.OK)
            {
                if (allowCancel)
                {
                    return null;
                }

                MessageBoxResult exit = WpfMessageBox.Show(
                    "You must select a folder to continue. Do you want to exit the application?",
                    "Vintage Story Mod Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (exit == MessageBoxResult.Yes)
                {
                    return null;
                }

                continue;
            }

            candidate = dialog.SelectedPath;
            if (validator(candidate, out string? normalized, out string? errorMessage))
            {
                return normalized;
            }

            WpfMessageBox.Show(errorMessage ?? "The selected folder is not valid.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static bool TryValidateDataDirectory(string? path, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "No folder was selected.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            errorMessage = "The folder path is invalid.";
            return false;
        }

        if (!Directory.Exists(normalizedPath))
        {
            errorMessage = "The folder does not exist.";
            return false;
        }

        bool hasClientSettings = File.Exists(Path.Combine(normalizedPath, "clientsettings.json"));
        bool hasMods = Directory.Exists(Path.Combine(normalizedPath, "Mods"));
        bool hasConfig = Directory.Exists(Path.Combine(normalizedPath, "ModConfig"));

        if (!hasClientSettings && !hasMods && !hasConfig)
        {
            errorMessage = "The folder does not appear to be a VintagestoryData directory.";
            return false;
        }

        return true;
    }

    private static bool TryValidateGameDirectory(string? path, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "No folder was selected.";
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            errorMessage = "The folder path is invalid.";
            return false;
        }

        if (File.Exists(candidate))
        {
            string? directory = Path.GetDirectoryName(candidate);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorMessage = "The folder path is invalid.";
                return false;
            }

            candidate = directory;
        }

        if (!Directory.Exists(candidate))
        {
            errorMessage = "The folder does not exist.";
            return false;
        }

        string? executable = GameDirectoryLocator.FindExecutable(candidate);
        if (executable is null)
        {
            errorMessage = "The folder does not contain a Vintage Story executable.";
            return false;
        }

        normalizedPath = candidate;
        return true;
    }

    private void ModsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            dataGrid.SelectedIndex = -1;
            dataGrid.UnselectAll();
            dataGrid.UnselectAllCells();
        }
    }

    private void ModsDataGridRow_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ShouldIgnoreRowSelection(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is not DataGridRow row || row.DataContext is not ModListItemViewModel mod)
        {
            return;
        }

        row.Focus();
        HandleModRowSelection(mod);
        e.Handled = true;
    }

    private void ModDatabasePageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { DataContext: ModListItemViewModel mod }
            && mod.OpenModDatabasePageCommand is ICommand command
            && command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }

    private async void RefreshModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.RefreshCommand == null)
        {
            return;
        }

        try
        {
            await _viewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to refresh mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void SelectDataFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? selected = PromptForDirectory(
            "Select your VintagestoryData folder",
            _dataDirectory,
            TryValidateDataDirectory,
            allowCancel: true);

        if (selected is null)
        {
            return;
        }

        if (string.Equals(selected, _dataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _dataDirectory = selected;
        _userConfiguration.SetDataDirectory(selected);
        await ReloadViewModelAsync();
    }

    private void SelectGameFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? selected = PromptForDirectory(
            "Select your Vintage Story installation folder",
            _gameDirectory,
            TryValidateGameDirectory,
            allowCancel: true);

        if (selected is null)
        {
            return;
        }

        if (string.Equals(selected, _gameDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _gameDirectory = selected;
        _userConfiguration.SetGameDirectory(selected);
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LaunchGameButton_OnClick(object sender, RoutedEventArgs e)
    {
        string? executable = GameDirectoryLocator.FindExecutable(_gameDirectory);
        if (executable is null)
        {
            WpfMessageBox.Show("The Vintage Story executable could not be found. Verify the game folder in File > Set Game Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to launch Vintage Story:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenModFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Mods"), "mods");
    }

    private void OpenConfigFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "ModConfig"), "config");
    }

    private void OpenLogsFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Logs"), "logs");
    }

    private static void OpenFolder(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            WpfMessageBox.Show($"The {description} folder is not available. Please verify the VintagestoryData folder from File > Set Data Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(path))
        {
            WpfMessageBox.Show($"The {description} folder could not be found at:\n{path}\nPlease verify the VintagestoryData folder from File > Set Data Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to open the {description} folder:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void PresetComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSavePresetButtonContent();

        if (PresetComboBox.SelectedItem is not ModPreset preset)
        {
            _pendingPresetReselection = null;
            return;
        }

        _pendingPresetReselection = null;

        await ApplyPresetAsync(preset);
    }

    private async void PresetComboBox_OnDropDownClosed(object? sender, EventArgs e)
    {
        if (_pendingPresetReselection is null)
        {
            return;
        }

        if (PresetComboBox.SelectedItem is not ModPreset preset)
        {
            _pendingPresetReselection = null;
            return;
        }

        if (!string.Equals(preset.Name, _pendingPresetReselection, StringComparison.OrdinalIgnoreCase))
        {
            _pendingPresetReselection = null;
            return;
        }

        _pendingPresetReselection = null;

        await ApplyPresetAsync(preset);
    }

    private void PresetComboBox_OnItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox comboBox)
        {
            return;
        }

        if (comboBox.SelectedItem is not ModPreset selected)
        {
            _pendingPresetReselection = null;
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            _pendingPresetReselection = null;
            return;
        }

        var container = ItemsControl.ContainerFromElement(comboBox, source) as ComboBoxItem;
        if (container?.DataContext is ModPreset preset &&
            string.Equals(preset.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
        {
            _pendingPresetReselection = preset.Name;
        }
        else
        {
            _pendingPresetReselection = null;
        }
    }

    private async Task ApplyPresetAsync(ModPreset preset)
    {
        if (_viewModel is null || _isApplyingPreset)
        {
            return;
        }

        _isApplyingPreset = true;
        try
        {
            await _viewModel.ApplyPresetAsync(preset.Name, preset.DisabledEntries);
        }
        finally
        {
            _isApplyingPreset = false;
            UpdateSavePresetButtonContent();
        }
    }

    private void SavePresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (IsDeletePresetMode())
        {
            if (PresetComboBox.SelectedItem is not ModPreset preset)
            {
                return;
            }

            if (_userConfiguration.RemovePreset(preset.Name))
            {
                RefreshPresetList();
                _viewModel.ReportStatus($"Deleted preset \"{preset.Name}\".");
            }

            return;
        }

        string? defaultName = (PresetComboBox.SelectedItem as ModPreset)?.Name;
        string? presetName = PromptForPresetName(defaultName);
        if (presetName is null)
        {
            return;
        }

        if (_userConfiguration.ContainsPreset(presetName))
        {
            MessageBoxResult overwrite = WpfMessageBox.Show(
                $"A preset named \"{presetName}\" already exists. Replace it?",
                "Vintage Story Mod Manager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (overwrite != MessageBoxResult.Yes)
            {
                return;
            }
        }

        IReadOnlyList<string> disabledEntries = _viewModel.GetCurrentDisabledEntries();
        _userConfiguration.SetPreset(presetName, disabledEntries);
        RefreshPresetList(presetName);
        _viewModel.ReportStatus($"Saved preset \"{presetName}\".");
    }

    private void SavePresetButton_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHoveringSavePresetButton = true;
        UpdateSavePresetButtonContent();
    }

    private void SavePresetButton_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHoveringSavePresetButton = false;
        UpdateSavePresetButtonContent();
    }

    private void Window_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            UpdateSavePresetButtonContent();
        }
    }

    private void Window_OnPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            UpdateSavePresetButtonContent();
        }
    }

    private bool IsDeletePresetMode()
    {
        return _isHoveringSavePresetButton
               && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
               && PresetComboBox.SelectedItem is ModPreset;
    }

    private void UpdateSavePresetButtonContent()
    {
        if (SavePresetButton == null)
        {
            return;
        }

        SavePresetButton.Content = IsDeletePresetMode() ? "Delete Preset" : "Save Preset";
    }

    private void AttachToModsView(ICollectionView modsView)
    {
        if (_modsCollection != null)
        {
            _modsCollection.CollectionChanged -= ModsView_OnCollectionChanged;
            _modsCollection = null;
        }

        if (modsView is INotifyCollectionChanged notify)
        {
            _modsCollection = notify;
            notify.CollectionChanged += ModsView_OnCollectionChanged;
        }

        ClearSelection(resetAnchor: true);
    }

    private void ModsView_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() => ClearSelection(resetAnchor: true));
    }

    private void HandleModRowSelection(ModListItemViewModel mod)
    {
        bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (isShiftPressed && _selectionAnchor is { } anchor)
        {
            ApplyRangeSelection(anchor, mod, isCtrlPressed);
            _selectionAnchor = mod;
            return;
        }

        if (isCtrlPressed)
        {
            if (_selectedMods.Contains(mod))
            {
                RemoveFromSelection(mod);
                _selectionAnchor = mod;
            }
            else
            {
                AddToSelection(mod);
                _selectionAnchor = mod;
            }

            return;
        }

        ClearSelection();
        AddToSelection(mod);
        _selectionAnchor = mod;
    }

    private void ApplyRangeSelection(ModListItemViewModel start, ModListItemViewModel end, bool preserveExisting)
    {
        List<ModListItemViewModel> mods = GetModsInViewOrder();
        int startIndex = mods.IndexOf(start);
        int endIndex = mods.IndexOf(end);

        if (startIndex < 0 || endIndex < 0)
        {
            if (!preserveExisting)
            {
                ClearSelection();
            }

            AddToSelection(end);
            return;
        }

        if (!preserveExisting)
        {
            ClearSelection();
        }

        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            AddToSelection(mods[i]);
        }
    }

    private List<ModListItemViewModel> GetModsInViewOrder()
    {
        if (_viewModel?.ModsView == null)
        {
            return new List<ModListItemViewModel>();
        }

        return _viewModel.ModsView.Cast<ModListItemViewModel>().ToList();
    }

    private void AddToSelection(ModListItemViewModel mod)
    {
        if (_selectedMods.Contains(mod))
        {
            return;
        }

        _selectedMods.Add(mod);
        mod.IsSelected = true;
    }

    private void RemoveFromSelection(ModListItemViewModel mod)
    {
        if (!_selectedMods.Remove(mod))
        {
            return;
        }

        mod.IsSelected = false;
    }

    private void ClearSelection(bool resetAnchor = false)
    {
        if (_selectedMods.Count > 0)
        {
            foreach (var mod in _selectedMods)
            {
                mod.IsSelected = false;
            }

            _selectedMods.Clear();
        }

        if (resetAnchor)
        {
            _selectionAnchor = null;
        }
    }

    private static bool ShouldIgnoreRowSelection(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ActiveToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingMultiToggle)
        {
            return;
        }

        if (sender is not ToggleSwitch { DataContext: ModListItemViewModel mod })
        {
            return;
        }

        if (!_selectedMods.Contains(mod) || _selectedMods.Count <= 1)
        {
            return;
        }

        bool desiredState = mod.IsActive;

        try
        {
            _isApplyingMultiToggle = true;

            foreach (var selected in _selectedMods)
            {
                if (ReferenceEquals(selected, mod))
                {
                    continue;
                }

                if (!selected.CanToggle || selected.IsActive == desiredState)
                {
                    continue;
                }

                selected.IsActive = desiredState;
            }
        }
        finally
        {
            _isApplyingMultiToggle = false;
        }
    }

    private string? PromptForPresetName(string? defaultName)
    {
        var dialog = new Window
        {
            Title = "Save Mod Preset",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false
        };

        var layout = new StackPanel
        {
            Margin = new Thickness(16),
            MinWidth = 320
        };

        var textBlock = new TextBlock
        {
            Text = "Enter a name for the preset:",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var textBox = new WpfTextBox
        {
            Text = defaultName ?? string.Empty,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var buttonsPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var saveButton = new WpfButton
        {
            Content = "Save",
            Width = 88,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };

        var cancelButton = new WpfButton
        {
            Content = "Cancel",
            Width = 88,
            IsCancel = true
        };

        buttonsPanel.Children.Add(saveButton);
        buttonsPanel.Children.Add(cancelButton);

        layout.Children.Add(textBlock);
        layout.Children.Add(textBox);
        layout.Children.Add(buttonsPanel);

        dialog.Content = layout;

        saveButton.IsEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
        textBox.TextChanged += (_, _) => saveButton.IsEnabled = !string.IsNullOrWhiteSpace(textBox.Text);

        dialog.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        saveButton.Click += (_, _) =>
        {
            dialog.DialogResult = true;
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = false;
            dialog.Close();
        };

        bool? result = dialog.ShowDialog();
        if (result == true)
        {
            string trimmed = textBox.Text.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopModsWatcher();
        base.OnClosed(e);
    }
}
